namespace News.Service;

using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Xml;
using CodeHollow.FeedReader;
using Dapper;
using FastMember;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using News.Service.Data;

sealed class Worker : BackgroundService
{
    private readonly DbDataSource db;
    private readonly ServiceOptions options;
    private readonly ILogger<Worker> log;

    public Worker(DbDataSource db, IOptions<ServiceOptions> options, ILogger<Worker> log)
    {
        this.db = db;
        this.options = options.Value;
        this.log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ImportOpmlAsync(stoppingToken);
        await UpdateFeedsPeriodicallyAsync(stoppingToken);
    }

    private async Task ImportOpmlAsync(CancellationToken cancellationToken)
    {
        if (!this.options.OpmlDirectory.Exists)
        {
            this.log.LogWarning("Opml directory '{opmlDirectory}' does not exist", this.options.OpmlDirectory);
            return;
        }

        foreach (var opmlFile in this.options.OpmlDirectory.EnumerateFiles("*.opml"))
        {
            try
            {
                this.log.LogInformation("Importing opml file '{fileName}'", opmlFile.Name);
                await ImportOpmlFileAsync(opmlFile, cancellationToken);
                this.log.LogInformation("Finished importing opml file '{fileName}'", opmlFile.Name);

                opmlFile.Delete();
            }
            catch (Exception x)
            {
                this.log.LogError(x, "Error importing opml file '{fileName}'", opmlFile.Name);
            }
        }
    }

    private async Task ImportOpmlFileAsync(FileInfo opmlFile, CancellationToken cancellationToken)
    {
        var userId = GetUserId(opmlFile);
        if (userId == default)
        {
            this.log.LogWarning("Opml file '{fileName}' has no user id", opmlFile.Name);
            throw new InvalidOperationException("Opml file has no user id");
        }

        var channels = ParseOpmlFile(opmlFile);
        if (channels.Length == 0)
        {
            this.log.LogWarning("Opml file '{fileName}' has no channels", opmlFile.Name);
            throw new InvalidOperationException("Opml file has no channels");
        }

        foreach (var channel in channels)
        {
            await ImportChannelAsync(channel, userId, cancellationToken);
        }
    }

    private static Guid GetUserId(FileInfo opmlFile)
    {
        var opmlFileName = opmlFile.Name.AsSpan();
        var lastDashIdx = opmlFileName.LastIndexOf('-');
        if (lastDashIdx < 0)
        {
            return default;
        }

        var userIdSpan = opmlFileName[..lastDashIdx];
        return Guid.TryParse(userIdSpan, out var userId) ? userId : default;
    }

    private async Task ImportChannelAsync(ChannelOutline channel, Guid userId, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            this.log.LogInformation("Importing channel '{channel}'", channel.Name);

            // upsert channel
            var channelId = await cnn.ExecuteScalarAsync<Guid>(
                """
                update rss.UserChannels
                set Name = @Name, Slug = @Slug
                output inserted.Id
                where Name = @Name or Slug = @Slug;

                if @@rowcount = 0
                begin
                    insert into rss.UserChannels (UserId, Name, Slug)
                    output inserted.Id
                    values (@UserId, @Name, @Slug);
                end;
                """, new { UserId = userId, channel.Name, Slug = channel.Text.ToLowerInvariant() }, tx);

            // merge feeds
            await ImportFeedsAsync(channel.Feeds, channelId, userId, tx, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            this.log.LogInformation("Finished importing channel '{channel}'", channel.Name);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error importing channel '{channel}'", channel.Name);
        }
    }

    private async Task ImportFeedsAsync(FeedOutline[] feeds, Guid channelId, Guid userId, DbTransaction tx, CancellationToken cancellationToken)
    {
        // create temp feeds tables
        await tx.Connection.ExecuteAsync(
            """
            create table #Feeds (
                Title nvarchar(1000) not null,
                Slug nvarchar(100) not null,
                Source nvarchar(850) not null primary key,
                Link nvarchar(850) not null
            );

            create table #ImportedFeeds (
                FeedId uniqueidentifier not null primary key,
                Source nvarchar(850) not null
            );
            """, transaction: tx);

        // bulk insert feeds
        using (var bulkCopy = new SqlBulkCopy((SqlConnection)tx.Connection!, SqlBulkCopyOptions.Default, (SqlTransaction)tx)
        {
            DestinationTableName = "#Feeds",
        })
        {
            using var feedReader = ObjectReader.Create(feeds,
                nameof(FeedOutline.Name), nameof(FeedOutline.Text),
                nameof(FeedOutline.XmlUrl), nameof(FeedOutline.Url));

            await bulkCopy.WriteToServerAsync(feedReader, cancellationToken);
        }

        // merge feeds
        await tx.Connection.ExecuteAsync(
            """
            merge rss.Feeds as tgt using #Feeds as src 
                on tgt.Source = src.Source
            when not matched by target then
                insert (Title, Source, Link)
                values (src.Title, src.Source, src.Link)
            when matched then
                update set tgt.Link = src.Link
            output inserted.Id as FeedId, inserted.Source
            into #ImportedFeeds;
            """, transaction: tx);

        // merge user feeds
        await tx.Connection.ExecuteAsync(
            """
            select imf.FeedId, isnull(uf.Slug, f.Slug) as Slug
            into #UserFeeds
            from #ImportedFeeds imf
            left outer join rss.UserFeeds uf on imf.FeedId = uf.FeedId
            left outer join #Feeds f on imf.Source = f.Source
            where uf.UserId = @UserId or uf.UserId is null;

            merge rss.UserFeeds as tgt using #UserFeeds as src
                on tgt.FeedId = src.FeedId and tgt.ChannelId = @ChannelId and tgt.UserId = @UserId
            when not matched then
                insert (UserId, ChannelId, FeedId, Slug)
                values (@UserId, @ChannelId, src.FeedId, src.Slug);
            """, new { UserId = userId, ChannelId = channelId }, tx);

        await tx.Connection.ExecuteAsync(
            """
            drop table #ImportedFeeds;
            drop table #Feeds;
            """, transaction: tx);
    }

    private ChannelOutline[] ParseOpmlFile(FileInfo opmlFile)
    {
        var opml = new XmlDocument();
        using (var opmlReader = opmlFile.OpenText())
        {
            opml.Load(opmlReader);
        }

        if (opml.DocumentElement is null || opml.DocumentElement.Name != "opml" || !opml.DocumentElement.HasChildNodes)
        {
            this.log.LogError("Opml file '{fileName}' is not an OPML file", opmlFile.Name);
            throw new InvalidOperationException("Opml file is not an OPML file");
        }

        // get <body> element
        var body = opml.DocumentElement.ChildNodes[1];
        if (body is null || body.Name != "body" || !body.HasChildNodes)
        {
            this.log.LogError("Opml file '{fileName}' has no <body> element or it has no child nodes", opmlFile.Name);
            throw new InvalidOperationException("Opml file has no <body> element or it has no child nodes");
        }

        // find channel <outline> elements
        var channels = new List<ChannelOutline>();
        foreach (var childNode in body.ChildNodes)
        {
            if (childNode is not XmlNode outline)
                continue;

            var channel = ChannelOutline.FromXml(outline);
            if (channel is not null)
            {
                channels.Add(channel);
            }
        }
        return channels.ToArray();
    }

    private async Task UpdateFeedsPeriodicallyAsync(CancellationToken cancellationToken)
    {
        var timer = new PeriodicTimer(this.options.UpdateInterval);
        do
        {
            try
            {
                this.log.LogInformation("Updating feeds at: {time}", DateTimeOffset.Now);

                await UpdateFeedsAsync(cancellationToken);
            }
            catch (Exception x)
            {
                this.log.LogError(x, "Error updating feeds");
            }
            finally
            {
                this.log.LogInformation("Finished updating feeds at: {time}", DateTimeOffset.Now);
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task UpdateFeedsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var feeds = await GetFeedsAsync(cancellationToken);
            foreach (var feed in feeds)
            {
                await UpdateFeedAsync(feed, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            this.log.LogError(ex, "Error updating feeds");
        }
    }

    private async Task<IEnumerable<DbFeed>> GetFeedsAsync(CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        var feeds = await cnn.QueryAsync<DbFeed>(
            """
            select f.Id, f.Source
            from rss.Feeds f
            order by f.Updated;
            """);

        return feeds;
    }

    private async Task UpdateFeedAsync(DbFeed feed, CancellationToken cancellationToken)
    {
        try
        {
            var update = await FeedReader.ReadAsync(feed.Source, cancellationToken);
            await MergeFeedUpdateAsync(feed, update, cancellationToken);
        }
        catch (Exception x)
        {
            this.log.LogError(x, "Error updating feed {FeedUrl}", feed.Source);
            await StoreFeedUpdateErrorAsync(feed, x, cancellationToken);
        }
    }

    private async Task MergeFeedUpdateAsync(DbFeed feed, Feed update, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                create table #Posts (
                    Id varchar(850) not null primary key,
                    Link nvarchar(850) not null,
                    Published datetimeoffset not null,
                    Title nvarchar(1000) not null,
                    Description nvarchar(max) null,
                    Author nvarchar(100) null,
                    Content nvarchar(max) null
                );
                """, transaction: tx);

            using (var bulkCopy = new SqlBulkCopy((SqlConnection)cnn, SqlBulkCopyOptions.Default, (SqlTransaction)tx)
            {
                DestinationTableName = "#Posts",
            })
            {
                using var postReader = ObjectReader.Create(
                    update.Items.Select(item => new FeedItemWrapper(item)),
                    nameof(FeedItemWrapper.Id),
                    nameof(FeedItemWrapper.Link),
                    nameof(FeedItemWrapper.Published),
                    nameof(FeedItemWrapper.Title),
                    nameof(FeedItemWrapper.Description),
                    nameof(FeedItemWrapper.Author),
                    nameof(FeedItemWrapper.Content));

                await bulkCopy.WriteToServerAsync(postReader, cancellationToken);
            }

            await cnn.ExecuteAsync(
                """
                set ansi_warnings off;

                merge rss.Posts as tgt using #Posts as src
                    on tgt.ExternalId = src.Id
                when matched and tgt.FeedId = @FeedId then
                    update set
                        Link = src.Link,
                        Published = src.Published,
                        Title = src.Title,
                        Description = src.Description,
                        Author = src.Author,
                        Content = src.Content
                when not matched then
                    insert (FeedId, ExternalId, Link, Published, Title, Description, Author, Content)
                    values (@FeedId, src.Id, src.Link, src.Published, src.Title, src.Description, src.Author, src.Content);

                set ansi_warnings on;
                """, new { FeedId = feed.Id }, tx);

            await cnn.ExecuteAsync(
                """
                drop table #Posts;
                """, transaction: tx);

            if (FeedUpdateHasInfo(update))
            {
                await cnn.ExecuteAsync(
                    """
                    update rss.Feeds
                    set
                        Updated = @Updated,
                        Title = @Title,
                        Description = @Description,
                        Link = @Link,
                        Error = null
                    where Id = @FeedId;
                    """, new
                    {
                        FeedId = feed.Id,
                        Updated = DateTimeOffset.Now,
                        update.Title,
                        update.Description,
                        update.Link
                    }, tx);
            }
            else
            {
                // some feeds are just broken
                await cnn.ExecuteAsync(
                    """
                    update rss.Feeds
                    set Updated = @Updated, Error = null
                    where Id = @FeedId;
                    """, new
                    {
                        FeedId = feed.Id,
                        Updated = DateTimeOffset.Now
                    }, tx);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error merging feed {FeedUrl}", feed.Source);

            await StoreFeedUpdateErrorAsync(feed, x, cancellationToken);
        }
    }

    private async Task StoreFeedUpdateErrorAsync(DbFeed feed, Exception error, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                update rss.Feeds
                set Updated = @Updated, Error = @Error
                where Id = @FeedId;
                """, new { FeedId = feed.Id, Updated = DateTimeOffset.Now, Error = error.Message }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error storing feed update error");
        }
    }

    private static bool FeedUpdateHasInfo(Feed update)
    {
        return
            !string.IsNullOrWhiteSpace(update.Title) &&
            !string.IsNullOrWhiteSpace(update.Description) &&
            !string.IsNullOrWhiteSpace(update.Link);
    }
}
