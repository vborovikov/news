namespace News.Service;

using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml;
using Brackets;
using Dapper;
using Data;
using Dodkin.Dispatch;
using FastMember;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Readability;
using Relay.RequestModel;
using Spryer;
using Storefront.UserAgent;
using Syndication;
using Syndication.Parser;

sealed class Worker : BackgroundService,
    IAsyncCommandHandler<LocalizeFeedsCommand>
{
    private const int MaxLocalizingPostCount = 10;
    private const int MaxLocalizingJobCount = 10;
    private static readonly TimeSpan LocalizingJobTimeout = TimeSpan.FromMinutes(3);

    private readonly ServiceOptions options;
    private readonly DbDataSource db;
    private readonly IHttpClientFactory web;
    private readonly IQueueRequestDispatcher usr;
    private readonly RequestHandler handler;
    private readonly ILogger log;
    private readonly ILogger wslog;
    private readonly Stopwatch busyMonitor;

    public Worker(IOptions<ServiceOptions> options,
        DbDataSource db, IHttpClientFactory web, IQueueRequestDispatcher usr,
        ILogger<Worker> log, ILogger<WindowShopper> wslog, ILogger<RequestHandler> rhlog)
    {
        this.options = options.Value;
        this.db = db;
        this.web = web;
        this.usr = usr;
        this.handler = new(this, this.options.Endpoint, rhlog);
        this.log = log;
        this.wslog = wslog;
        this.busyMonitor = new Stopwatch();
    }

    Task IAsyncCommandHandler<LocalizeFeedsCommand>.ExecuteAsync(LocalizeFeedsCommand command)
    {
        throw new NotImplementedException();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.WhenAll(
            AggregateNewsAsync(stoppingToken),
            this.handler.ProcessAsync(stoppingToken));
    }

    private async Task AggregateNewsAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.options.UpdateInterval);
        do
        {
            try
            {
                this.log.LogInformation("Aggregating news at: {time}", DateTimeOffset.Now);
                this.busyMonitor.Restart();

                await ImportFeedsAsync(stoppingToken);
                await UpdateFeedsAsync(stoppingToken);
                await SafeguardFeedsAsync(stoppingToken);
            }
            finally
            {
                this.busyMonitor.Stop();
                this.log.LogInformation("Finished aggregating news at: {time} ({busy})", DateTimeOffset.Now, this.busyMonitor.Elapsed);
                if (this.busyMonitor.Elapsed > this.options.UpdateInterval)
                {
                    this.log.LogWarning("News aggregation took too long: {busy}", this.busyMonitor.Elapsed);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SafeguardFeedsAsync(CancellationToken stoppingToken)
    {
        try
        {
            this.log.LogInformation("Safeguarding feeds at: {time}", DateTimeOffset.Now);
            var feeds = await GetFeedsToSafeguardAsync(stoppingToken);
            var total = feeds.Count();
            var count = 0;
            await Parallel.ForEachAsync(feeds, stoppingToken, async (feed, cancellationToken) =>
            {
                this.log.LogDebug("Safeguarding feed ({count}/{total}) {feedSource}", Interlocked.Increment(ref count), total, feed.Source);
                await SafeguardFeedAsync(feed, cancellationToken);
            });
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            this.log.LogError(x, "Error safeguarding feeds");
        }
        finally
        {
            this.log.LogInformation("Finished safeguarding feeds at: {time}", DateTimeOffset.Now);
        }
    }

    private async Task SafeguardFeedAsync(DbFeed feed, CancellationToken cancellationToken)
    {
        if (feed.Safeguards.HasFlag(FeedSafeguard.ContentExtractor) ||
            feed.Safeguards.HasFlag(FeedSafeguard.ImageLinkFixer) ||
            feed.Safeguards.HasFlag(FeedSafeguard.PostLinkFixer))
        {
            await LocalizeFeedAsync(feed, LocalizingJobTimeout, cancellationToken);
        }

        await SanitizeFeedAsync(feed, cancellationToken);
    }

    private async Task LocalizeFeedAsync(DbFeed feed, TimeSpan desiredDuration, CancellationToken cancellationToken)
    {
        var jobCount = 0;
        var stopwatch = new Stopwatch();
        do
        {
            try
            {
                this.log.LogInformation("Take #{jobCount} localizing feed {feedSource}", jobCount + 1, feed.Source);
                stopwatch.Restart();

                var localized = await TryLocalizeRecentPostsAsync(feed, cancellationToken);
                if (localized is false)
                {
                    this.log.LogDebug("No more posts localized for feed {feedSource}", feed.Source);
                    break;
                }
            }
            catch (Exception x) when (x is not OperationCanceledException)
            {
                this.log.LogError(x, "Error localizing feed {feedSource}", feed.Source);
                throw;
            }
            finally
            {
                stopwatch.Stop();
                this.log.LogInformation("Localizing feed {feedSource} took {time}", feed.Source, stopwatch.Elapsed);
            }
        }
        while (jobCount++ <= MaxLocalizingJobCount && stopwatch.Elapsed < desiredDuration);
    }

    private async Task<bool> TryLocalizeRecentPostsAsync(DbFeed feed, CancellationToken cancellationToken)
    {
        // get 10 most recent non-localized posts
        var posts = await GetRecentNonLocalizedPostsAsync(feed, MaxLocalizingPostCount, cancellationToken);
        if (!posts.Any())
            return false;

        using var postClient = this.web.CreateClient(HttpClients.Post);
        using var imageClient = this.web.CreateClient(HttpClients.Image);
        var windowShopper = new WindowShopper(postClient, this.usr, this.wslog);
        foreach (var post in posts)
        {
            try
            {
                if (feed.Safeguards.HasFlag(FeedSafeguard.ContentExtractor))
                {
                    // download post contents
                    await DownloadPostContentAsync(post, windowShopper, cancellationToken);
                    await LocalizePostAsync(post, cancellationToken);
                }

                if (feed.Safeguards.HasFlag(FeedSafeguard.ImageLinkFixer))
                {
                    // download images
                    await DownloadPostImagesAsync(post, imageClient, cancellationToken);
                }

                if (feed.Safeguards.HasFlag(FeedSafeguard.PostLinkFixer))
                {
                    // fix links to other posts
                    await FixPostLinksAsync(post, cancellationToken);
                }
            }
            catch (Exception x) when (x is not OperationCanceledException)
            {
                this.log.LogError(x, "Error localizing post '{postLink}' from feed {feedSource}", post.Link, feed.Source);
            }
        }

        return true;
    }

    private Task FixPostLinksAsync(DbPost post, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private Task DownloadPostImagesAsync(DbPost post, HttpClient client, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task DownloadPostContentAsync(DbPost post, WindowShopper client, CancellationToken cancellationToken)
    {
        string postContent;
        try
        {
            postContent = await client.GetSourceAsync(new(post.Link), ResourceAccess.WebRequest, cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await StorePostUpdateErrorAsync(post, x, cancellationToken);
            throw;
        }
        
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                update rss.Posts
                set LocalContentSource = @LocalContentSource
                where Id = @Id;
                """, new
                {
                    post.Id,
                    LocalContentSource = postContent.AsNVarChar(),
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task StorePostUpdateErrorAsync(DbPost post, Exception error, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                update rss.Posts
                set Status = @Status, Error = @Error
                where Id = @PostId;
                """, new
                {
                    PostId = post.Id,
                    Status = GetPostStatus(error, post.Status),
                    Error = error.Message.AsNVarChar(2000),
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error storing update status for post '{postLink}'", post.Link);
        }
    }

    private static DbEnum<PostStatus> GetPostStatus(Exception error, PostStatus status)
    {
        return status | PostStatus.SkipUpdate;
    }

    private async Task LocalizePostAsync(DbPost post, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        var postSource = await cnn.QueryFirstOrDefaultAsync<string>(
            """
            select LocalContentSource
            from rss.Posts
            where Id = @Id
            """, new { post.Id });
        if (string.IsNullOrWhiteSpace(postSource))
            return;

        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            var postArticle = DocumentReader.ParseArticle(postSource, new Uri(post.Link));
            await cnn.ExecuteAsync(
                """
                update rss.Posts
                set LocalContent = @LocalContent, LocalDescription = @LocalDescription
                where Id = @Id;
                """, new
                {
                    post.Id,
                    LocalContent = postArticle.Content.ToText(),
                    LocalDescription = postArticle.Excerpt
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error localizing post content for '{postLink}'", post.Link);
            throw;
        }
    }

    private async Task<IEnumerable<DbPost>> GetRecentNonLocalizedPostsAsync(DbFeed feed, int postCount, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        try
        {
            var posts = await cnn.QueryAsync<DbPost>(
                """
                select top (@PostCount) p.Id, p.Link, p.Title, p.Status, p.Description, p.Content
                from rss.Posts p
                where p.FeedId = @FeedId and p.LocalContentSource is null and p.Status not like '%SKIP%' 
                order by p.Published desc;
                """, new { FeedId = feed.Id, PostCount = postCount });

            return posts;
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            this.log.LogError(x, "Error getting recent non-localized posts for feed {feedSource}", feed.Source);
            throw;
        }
    }

    private async Task SanitizeFeedAsync(DbFeed feed, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            var posts = await cnn.QueryAsync<DbPost>(
                """
                select p.Id, p.Link, p.Title, p.Status,
                    isnull(p.LocalDescription, p.Description) as Description,
                    isnull(p.LocalContent, p.Content) as Content
                from rss.Posts p
                where p.FeedId = @FeedId and p.SafeContent is null;
                """, new { FeedId = feed.Id }, tx);

            var total = posts.Count();
            var count = 0;
            await Parallel.ForEachAsync(posts, cancellationToken, async (post, cancellationToken) =>
            {
                this.log.LogDebug("Safeguarding post '{postTitle}' ({count}/{total}) {feedSource}",
                    post.Title, Interlocked.Increment(ref count), total, feed.Source);
                await SanitizePostAsync(post, feed, tx, cancellationToken);
            });

            await tx.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogDebug("Safeguarding feed {feedSource} was canceled", feed.Source);
            throw;
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error safeguarding feed {feedSource}", feed.Source);
        }
    }

    private static async Task SanitizePostAsync(DbPost post, DbFeed feed, DbTransaction tx, CancellationToken cancellationToken)
    {
        var safeContent = SanitizeContent(post.Content, feed.Safeguards);

        var safeDescription = feed.Safeguards.HasFlag(FeedSafeguard.DescriptionReplacer) ? post.Content : post.Description;
        if (!string.IsNullOrWhiteSpace(safeDescription))
        {
            safeDescription = SanitizeDescription(safeDescription, feed.Safeguards);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await tx.Connection!.ExecuteAsync(
            """
            update rss.Posts
            set SafeContent = @SafeContent, SafeDescription = @SafeDescription
            where Id = @Id;
            """, new { post.Id, SafeContent = safeContent, SafeDescription = safeDescription }, tx);
    }

    private static string SanitizeDescription(string text, FeedSafeguard safeguards)
    {
        var html = Document.Html.Parse(text);

        if (safeguards.HasFlag(FeedSafeguard.DescriptionImageRemover))
        {
            var imgElements = html.Find("//img").ToArray();
            foreach (var img in imgElements)
            {
                img.TryDelete(deleteEmptyAncestors: true);
            }
        }

        SanitizeCommon(html, safeguards);

        return html.ToText();
    }

    private static string SanitizeContent(string text, FeedSafeguard safeguards)
    {
        var html = Document.Html.Parse(text);

        SanitizeCommon(html, safeguards);

        return html.ToText();
    }

    private static void SanitizeCommon(Document html, FeedSafeguard safeguards)
    {
        if (safeguards.HasFlag(FeedSafeguard.LastParaTrimmer))
        {
            // remove any inline content at the end
            if (html.LastOrDefault() is Element { Layout: FlowLayout.Inline } lastElement)
            {
                var firstElement = html.First();
                while (lastElement != firstElement)
                {
                    if (lastElement is Tag { Layout: FlowLayout.Block } tag && tag.Name != "hr")
                    {
                        break;
                    }

                    if (!lastElement.TryDelete())
                        break;
                    lastElement = html.Last();
                }
            }

            // remove last paragraph
            if (html.LastOrDefault() is Tag { Name: "p" } para)
            {
                para.TryDelete();
            }
        }
    }

    private async Task<IEnumerable<DbFeed>> GetFeedsToSafeguardAsync(CancellationToken stoppingToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(stoppingToken);
        var feeds = await cnn.QueryAsync<DbFeed>(
            """
            select f.Id, f.Source, f.Status, f.Safeguards
            from rss.Feeds f
            where f.Status not like '%SKIP%' and f.Safeguards not like 'OK'
            order by f.Updated;
            """);
        return feeds;
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

            var feeds = await GetFeedsToUpdateAsync(cancellationToken);
            var client = this.web.CreateClient(HttpClients.Feed);
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
        await tx.Connection!.ExecuteAsync(
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
        await tx.Connection!.ExecuteAsync(
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
        await tx.Connection!.ExecuteAsync(
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

        await tx.Connection!.ExecuteAsync(
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

    private async Task<IEnumerable<DbFeed>> GetFeedsToUpdateAsync(CancellationToken cancellationToken)
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
            if (feed.Status.HasFlag(FeedStatus.UserAgent))
            {
                var feedData = await this.usr.GetStringAsync(feed.Source, cancellationToken);
                if (!string.IsNullOrWhiteSpace(feedData))
                {
                    try
                    {
                        update = Feed.FromString(feedData);
                    }
                    catch (Exception)
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
                    response.StatusCode == HttpStatusCode.Redirect ||
                    response.StatusCode == HttpStatusCode.PermanentRedirect)
                {
                    var feedNewSource = response.Headers.Location?.ToString();
                    if (!string.IsNullOrWhiteSpace(feedNewSource))
                    {
                        this.log.LogDebug("Updating feed source from {feedUrl} to {feedNewUrl}", feed.Source, feedNewSource);
                        feed = feed with { Source = feedNewSource };
                    }
                }
                response.EnsureSuccessStatusCode();

                var feedData = await response.Content.ReadAsStreamAsync(cancellationToken);
                try
                {
                    update = await Feed.FromStreamAsync(feedData, cancellationToken);
                }
                catch (HtmlContentDetectedException x)
                {
                    feed = MaybeUpdateFeedSource(feed, x.FeedLinks);
                    throw;
                }
            }

            await MergeFeedUpdateAsync(feed, update, cancellationToken);
        }
        // HttpClient throws a TimeoutException wrapped in TaskCanceledException so we must check that CancellationToken is not ours
        catch (Exception x) when (x is not OperationCanceledException oce || oce.CancellationToken != cancellationToken)
        {
            this.log.LogError(x, "Error updating feed {feedUrl}", feed.Source);
            await StoreFeedUpdateErrorAsync(feed, x, cancellationToken);
        }
    }

    private DbFeed MaybeUpdateFeedSource(DbFeed feed, string feedData)
    {
        var feedLinks = FeedReader.ParseFeedUrlsFromHtml(feedData);
        return MaybeUpdateFeedSource(feed, feedLinks);
    }

    private DbFeed MaybeUpdateFeedSource(DbFeed feed, IEnumerable<HtmlFeedLink> feedLinks)
    {
        var feedLink =
            feedLinks.FirstOrDefault(fl => fl.FeedType != FeedType.Unknown) ??
            feedLinks.FirstOrDefault();
        if (feedLink is not null)
        {
            feedLink = FeedReader.GetAbsoluteFeedUrl(feed.Source, feedLink);
            if (Uri.IsWellFormedUriString(feedLink.Url, UriKind.Absolute))
            {
                this.log.LogDebug("Updating feed source from {feedUrl} to {feedNewUrl}", feed.Source, feedLink.Url);
                feed = feed with { Source = feedLink.Url };
            }
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
                if (feed.Status.HasFlag(FeedStatus.DistinctId))
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
                    Source = feed.Source.AsNVarChar(850),
                    Updated = DateTimeOffset.Now,
                    Status = GetFeedStatus(error, feed.Status),
                    Error = error.Message.AsNVarChar(2000)
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error storing feed update error");
        }
    }

    private static DbEnum<FeedStatus> GetFeedStatus(Exception error, FeedStatus prevStatus)
    {
        if (error is SqlException { Number: 2627 })
        {
            return
                prevStatus.HasFlag(FeedStatus.DistinctId) ? FeedStatus.SkipUpdate :
                prevStatus.HasFlag(FeedStatus.UniqueId) ? FeedStatus.DistinctId | prevStatus :
                FeedStatus.UniqueId | prevStatus;
        }
        if (error is HttpRequestException httpEx)
        {
            return
                prevStatus.HasFlag(FeedStatus.HttpError) ? FeedStatus.SkipUpdate :
                httpEx.StatusCode == HttpStatusCode.Unauthorized || httpEx.StatusCode == HttpStatusCode.Forbidden ?
                prevStatus.HasFlag(FeedStatus.UserAgent) ? FeedStatus.SkipUpdate :
                FeedStatus.UserAgent | prevStatus :
                FeedStatus.HttpError | prevStatus;
        }
        if (error is FeedTypeNotSupportedException or HtmlContentDetectedException)
        {
            return prevStatus.HasFlag(FeedStatus.HtmlResponse) ? FeedStatus.SkipUpdate :
                FeedStatus.HtmlResponse | prevStatus;
        }
        if (error is TimeoutException)
        {
            if (prevStatus.HasFlag(FeedStatus.UserAgent))
            {
                return FeedStatus.SkipUpdate;
            }
        }

        return prevStatus;
    }
}
