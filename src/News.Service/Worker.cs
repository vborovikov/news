namespace News.Service;

using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Parser;
using Dapper;
using FastMember;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using News.Service.Data;
using Spryer;

sealed class Worker : BackgroundService
{
    private readonly ServiceOptions options;
    private readonly DbDataSource db;
    private readonly IHttpClientFactory web;
    private readonly UserAgent usr;
    private readonly ILogger<Worker> log;

    public Worker(IOptions<ServiceOptions> options, DbDataSource db, IHttpClientFactory web, UserAgent usr, ILogger<Worker> log)
    {
        this.options = options.Value;
        this.db = db;
        this.web = web;
        this.usr = usr;
        this.log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(this.options.UpdateInterval);
        do
        {
            await ImportFeedsAsync(stoppingToken);
            await UpdateFeedsAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ImportFeedsAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.log.LogInformation("Importing feeds at: {time}", DateTimeOffset.Now);

            if (!this.options.OpmlDirectory.Exists)
            {
                this.log.LogWarning("OPML directory '{opmlDirectory}' does not exist", this.options.OpmlDirectory);
                return;
            }

            foreach (var opmlFile in this.options.OpmlDirectory.EnumerateFiles("*.opml"))
            {
                try
                {
                    this.log.LogInformation("Importing OPML file '{fileName}'", opmlFile.Name);
                    await ImportOpmlFileAsync(opmlFile, cancellationToken);
                    this.log.LogInformation("Finished importing OPML file '{fileName}'", opmlFile.Name);

                    opmlFile.Delete();
                }
                catch (Exception x) when (x is not OperationCanceledException)
                {
                    this.log.LogError(x, "Error importing OPML file '{fileName}'", opmlFile.Name);
                }
            }
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            this.log.LogError(x, "Error importing feeds");
        }
        finally
        {
            this.log.LogInformation("Finished importing feeds at: {time}", DateTimeOffset.Now);
        }
    }

    private async Task UpdateFeedsAsync(CancellationToken cancellationToken)
    {
        try
        {
            this.log.LogInformation("Updating feeds at: {time}", DateTimeOffset.Now);

            var client = this.web.CreateClient("Feed");
            var feeds = await GetFeedsAsync(cancellationToken);
            var total = feeds.Count();
            var count = 0;
            await Parallel.ForEachAsync(feeds, cancellationToken, async (feed, cancellationToken) =>
            {
                this.log.LogDebug("Updating feed ({count}/{total}) {feedUrl}", Interlocked.Increment(ref count), total, feed.Source);
                await UpdateFeedAsync(feed, client, cancellationToken);
            });
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            this.log.LogError(x, "Error updating feeds");
        }
        finally
        {
            this.log.LogInformation("Finished updating feeds at: {time}", DateTimeOffset.Now);
        }
    }

    private async Task ImportOpmlFileAsync(FileInfo opmlFile, CancellationToken cancellationToken)
    {
        var userId = GetUserId(opmlFile);
        if (userId == default)
        {
            this.log.LogWarning("OPML file '{fileName}' has no user id", opmlFile.Name);
            throw new InvalidOperationException("OPML file has no user id");
        }

        var channels = ParseOpmlFile(opmlFile);
        if (channels.Length == 0)
        {
            this.log.LogWarning("OPML file '{fileName}' has no channels", opmlFile.Name);
            throw new InvalidOperationException("OPML file has no channels");
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
            this.log.LogDebug("Importing channel '{channel}'", channel.Name);

            // upsert channel
            var channelId = await cnn.ExecuteScalarAsync<Guid>(
                """
                merge into rss.UserChannels with (holdlock) as target
                using (values (@UserId, @Name, @Slug)) as source (UserId, Name, Slug)
                on (target.UserId = source.UserId and (target.Name = source.Name or target.Slug = source.Slug))
                when matched then
                    update set target.Name = source.Name, target.Slug = source.Slug
                when not matched then
                    insert (UserId, Name, Slug)
                    values (source.UserId, source.Name, source.Slug)
                output inserted.Id;
                """, new { UserId = userId, channel.Name, Slug = channel.Text.ToLowerInvariant() }, tx);

            // merge feeds
            await ImportFeedsAsync(channel.Feeds, channelId, userId, tx, cancellationToken);

            await tx.CommitAsync(cancellationToken);
            this.log.LogDebug("Finished importing channel '{channel}'", channel.Name);
        }
        catch (OperationCanceledException)
        {
            this.log.LogDebug("Import of channel '{channel}' was cancelled", channel.Name);
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error importing channel '{channel}'", channel.Name);
            throw;
        }
    }

    private static async Task ImportFeedsAsync(FeedOutline[] feeds, Guid channelId, Guid userId, DbTransaction tx, CancellationToken cancellationToken)
    {
        // create temp feeds tables
        await tx.Connection.ExecuteAsync(
            """
            create table #Feeds (
                Title nvarchar(1000) collate database_default not null,
                Slug varchar(100) collate database_default not null,
                Source nvarchar(850) collate database_default not null primary key,
                Link nvarchar(850) collate database_default not null
            );

            create table #ImportedFeeds (
                FeedId uniqueidentifier not null primary key,
                Source nvarchar(850) collate database_default not null
            );
            """, transaction: tx);

        // bulk insert feeds
        using (var bulkCopy = new SqlBulkCopy((SqlConnection)tx.Connection!, SqlBulkCopyOptions.Default, (SqlTransaction)tx)
        {
            DestinationTableName = "#Feeds",
            EnableStreaming = true,
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
            merge into rss.Feeds with (holdlock) as tgt
            using #Feeds as src on tgt.Source = src.Source
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
            select imf.FeedId, isnull(uf.Slug, f.Slug) as Slug, isnull(uf.Title, f.Title) as Title
            into #UserFeeds
            from #ImportedFeeds imf
            left outer join rss.UserFeeds uf on imf.FeedId = uf.FeedId
            left outer join #Feeds f on imf.Source = f.Source
            where uf.UserId = @UserId or uf.UserId is null;

            merge into rss.UserFeeds with (holdlock) as tgt
            using #UserFeeds as src on tgt.FeedId = src.FeedId and tgt.UserId = @UserId
            when not matched then
                insert (UserId, ChannelId, FeedId, Slug, Title)
                values (@UserId, @ChannelId, src.FeedId, src.Slug, src.Title);
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
            this.log.LogError("File '{fileName}' is not an OPML file", opmlFile.Name);
            throw new InvalidOperationException("File is not an OPML file");
        }

        // get <body> element
        var body = opml.DocumentElement.ChildNodes[1];
        if (body is null || body.Name != "body" || !body.HasChildNodes)
        {
            this.log.LogError("OPML file '{fileName}' has no <body> element or it has no child nodes", opmlFile.Name);
            throw new InvalidOperationException("OPML file has no <body> element or it has no child nodes");
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

    private async Task<IEnumerable<DbFeed>> GetFeedsAsync(CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        var feeds = await cnn.QueryAsync<DbFeed>(
            """
            select f.Id, f.Source, f.Status
            from rss.Feeds f
            where f.Status not like '%SKIP%' --and f.Status not like '%HTTP%'
            order by f.Updated;
            """);

        return feeds;
    }

    private async Task UpdateFeedAsync(DbFeed feed, HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            Feed? update = null;
            if (feed.Status.HasFlag(FeedUpdateStatus.UserAgent))
            {
                var feedData = await this.usr.GetStringAsync(feed.Source, cancellationToken);
                if (!string.IsNullOrWhiteSpace(feedData))
                {
                    try
                    {
                        update = FeedReader.ReadFromString(feedData);
                    }
                    catch (XmlException)
                    {
                        feed = MaybeUpdateFeedSource(feed, feedData);
                        throw;
                    }
                }
            }
            if (update is null)
            {
                using var response = await client.GetAsync(feed.Source, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Moved ||
                    response.StatusCode == HttpStatusCode.MovedPermanently ||
                    response.StatusCode == HttpStatusCode.PermanentRedirect)
                {
                    var feedNewSource = response.Headers.Location?.ToString();
                    if (!string.IsNullOrWhiteSpace(feedNewSource))
                    {
                        feed = feed with { Source = feedNewSource };
                    }
                }
                response.EnsureSuccessStatusCode();

                var feedData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                try
                {
                    update = FeedReader.ReadFromByteArray(feedData);
                }
                catch (XmlException)
                {
                    feed = MaybeUpdateFeedSource(feed, Encoding.UTF8.GetString(feedData));
                    throw;
                }
            }

            await MergeFeedUpdateAsync(feed, update, cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            this.log.LogError(x, "Error updating feed {feedUrl}", feed.Source);
            await StoreFeedUpdateErrorAsync(feed, x, cancellationToken);
        }
    }

    private static DbFeed MaybeUpdateFeedSource(DbFeed feed, string feedData)
    {
        var feedLinks = FeedReader.ParseFeedUrlsFromHtml(feedData);
        var feedLink =
            feedLinks.FirstOrDefault(fl => fl.FeedType != FeedType.Unknown) ??
            feedLinks.FirstOrDefault();
        if (feedLink is not null && Uri.IsWellFormedUriString(feedLink.Url, UriKind.Absolute))
        {
            feed = feed with { Source = feedLink.Url };
        }

        return feed;
    }

    private async Task MergeFeedUpdateAsync(DbFeed feed, Feed update, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            var feedUpdate = new FeedWrapper(update, feed);

            await cnn.ExecuteAsync(
                """
                create table #Posts (
                    Id varchar(850) collate database_default not null primary key,
                    Link nvarchar(max) collate database_default not null,
                    Slug varchar(100) collate database_default not null,
                    Published datetimeoffset not null,
                    Title nvarchar(1000) collate database_default not null,
                    Description nvarchar(max) collate database_default null,
                    Author nvarchar(100) collate database_default null,
                    Content nvarchar(max) collate database_default null
                );
                """, transaction: tx);

            using (var bulkCopy = new SqlBulkCopy((SqlConnection)cnn, SqlBulkCopyOptions.Default, (SqlTransaction)tx)
            {
                DestinationTableName = "#Posts",
                EnableStreaming = true,
            })
            {
                var feedItems = update.Items.Select(item => new FeedItemWrapper(item, feedUpdate));
                if (feed.Status.HasFlag(FeedUpdateStatus.DistinctId))
                {
                    feedItems = feedItems.DistinctBy(fi => fi.Id);
                }

                using var postReader = ObjectReader.Create(
                    feedItems,
                    nameof(FeedItemWrapper.Id),
                    nameof(FeedItemWrapper.Link),
                    nameof(FeedItemWrapper.Slug),
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

                merge into rss.Posts with (holdlock) as tgt
                using #Posts as src on tgt.ExternalId = src.Id
                when matched and tgt.FeedId = @FeedId then
                    update set
                        Link = src.Link,
                        Slug = src.Slug,
                        Published = src.Published,
                        Title = src.Title,
                        Description = src.Description,
                        Author = src.Author,
                        Content = src.Content
                when not matched then
                    insert (FeedId, ExternalId, Link, Slug, Published, Title, Description, Author, Content)
                    values (@FeedId, src.Id, src.Link, src.Slug, src.Published, src.Title, src.Description, src.Author, src.Content);

                set ansi_warnings on;
                """, new { FeedId = feed.Id }, tx);

            await cnn.ExecuteAsync(
                """
                drop table #Posts;
                """, transaction: tx);

            await cnn.ExecuteAsync(
                """
                set ansi_warnings off;
                
                update rss.Feeds
                set
                    Updated = @Updated,
                    Title = @Title,
                    Description = @Description,
                    Link = @Link,
                    Error = null
                where Id = @FeedId;

                set ansi_warnings on;
                """, new
                {
                    FeedId = feed.Id,
                    Updated = DateTimeOffset.Now,
                    feedUpdate.Title,
                    feedUpdate.Description,
                    feedUpdate.Link
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            this.log.LogDebug("Updating feed {feedUrl} was cancelled", feed.Source);
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error merging feed {feedUrl}", feed.Source);

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
                set Updated = @Updated, Source = @Source, Status = @Status, Error = @Error
                where Id = @FeedId;
                """, new
                {
                    FeedId = feed.Id,
                    Source = feed.Source,
                    Updated = DateTimeOffset.Now,
                    Status = GetStatus(error, feed.Status),
                    Error = error.Message
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error storing feed update error");
        }
    }

    private static DbEnum<FeedUpdateStatus> GetStatus(Exception error, FeedUpdateStatus prevStatus)
    {
        if (error is SqlException sqlEx && sqlEx.Number == 2627)
        {
            return
                prevStatus.HasFlag(FeedUpdateStatus.DistinctId) ? FeedUpdateStatus.SkipUpdate :
                prevStatus.HasFlag(FeedUpdateStatus.UniqueId) ? FeedUpdateStatus.DistinctId | prevStatus :
                FeedUpdateStatus.UniqueId | prevStatus;
        }
        if (error is HttpRequestException httpEx)
        {
            return
                prevStatus.HasFlag(FeedUpdateStatus.HttpError) ? FeedUpdateStatus.SkipUpdate :
                httpEx.StatusCode == HttpStatusCode.Unauthorized || httpEx.StatusCode == HttpStatusCode.Forbidden ?
                prevStatus.HasFlag(FeedUpdateStatus.UserAgent) ? FeedUpdateStatus.SkipUpdate :
                FeedUpdateStatus.UserAgent | prevStatus :
                FeedUpdateStatus.HttpError | prevStatus;
        }
        if (error is XmlException || error is FeedTypeNotSupportedException)
        {
            return prevStatus.HasFlag(FeedUpdateStatus.HtmlResponse) ? FeedUpdateStatus.SkipUpdate :
                FeedUpdateStatus.HtmlResponse | prevStatus;
        }

        return prevStatus;
    }
}
