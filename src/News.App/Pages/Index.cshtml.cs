namespace News.App.Pages;

using System.Data.Common;
using Dapper;
using Data;
using Microsoft.AspNetCore.Mvc;
using Relay.InteractionModel;
using Shared;
using Spryer;

public class IndexModel : AppPageModel
{
    private readonly DbDataSource db;
    private readonly ILogger<IndexModel> log;

    public IndexModel(DbDataSource db, ILogger<IndexModel> log)
    {
        this.db = db;
        this.log = log;
    }

    public GranularityLevel Granularity { get; private set; } = GranularityLevel.None;

    public IEnumerable<RssChannel> Channels { get; private set; } = [];

    public IEnumerable<RssPostRef> SimilarPosts { get; private set; } = [];

    public async Task<IActionResult> OnGet([FromQuery] PageRequest page, string? channel = null, string? feed = null,
        int year = 0, int month = 0, int day = 0, string? post = null,
        CancellationToken cancellationToken = default)
    {
        if (this.UserId == default)
        {
            // user not logged in
            return RedirectToPage("Account/Login", new { area = "Identity" });
        }

        string sql;

        // today
        var minDate = new DateTimeOffset(DateTimeOffset.Now.Date, DateTimeOffset.Now.Offset);
        var maxDate = DateTimeOffset.Now;
        if (year > 0)
        {
            this.Granularity = string.IsNullOrEmpty(post) ? GranularityLevel.Posts : GranularityLevel.Post;

            // tweaking dates when showing posts
            if (day > 0)
            {
                // one day
                minDate = new DateTimeOffset(year, month, day, hour: 0, minute: 0, second: 0, minDate.Offset);
                maxDate = minDate.AddDays(1);
            }
            else if (month > 0)
            {
                // one month
                minDate = new DateTimeOffset(year, month, day: 1, hour: 0, minute: 0, second: 0, minDate.Offset);
                maxDate = minDate.AddMonths(1);
            }
            else
            {
                // one year
                minDate = new DateTimeOffset(year, month: 1, day: 1, hour: 0, minute: 0, second: 0, minDate.Offset);
                maxDate = minDate.AddYears(1);
            }

            if (this.Granularity == GranularityLevel.Posts)
            {
                sql =
                    """
                    select uc.Id as ChannelId, uc.Name, uc.Slug,
                        json_query((
                            select uf.FeedId, uf.Title, uf.Slug, uf.Description,
                                uf.Link, uf.Updated, uf.Scheduled, uf.Error,
                                (select max(p.Published) 
                                 from rss.Posts p 
                                 where p.FeedId = uf.FeedId) as LastPublished,
                                json_query((
                                    select p.PostId, p.Title, p.Published, p.Description, p.Link, p.Slug, 
                                        p.IsRead, p.IsFavorite, p.Author, p.Content
                                    from rss.AppPosts p
                                    /**search-join**/
                                    where p.FeedId = uf.FeedId and p.Published >= @MinDate and p.Published <= @MaxDate
                                        /**filter-where-expr**/
                                    order by p.Published desc
                                    offset @SkipCount rows fetch next @TakeCount rows only
                                    for json path
                                )) as Posts
                            from rss.AppFeeds uf
                            where uf.UserId = @UserId and uf.ChannelId = uc.Id and uf.Slug = @FeedSlug
                            for json path
                        )) as Feeds
                    from rss.UserChannels uc
                    where uc.UserId = @UserId and uc.Slug = @ChannelSlug
                    for json path;
                    """;
            }
            else
            {
                sql =
                    """
                    select uc.Id as ChannelId, uc.Name, uc.Slug,
                        json_query((
                            select uf.FeedId, uf.Title, uf.Slug, uf.Description,
                                uf.Link, uf.Updated, uf.Scheduled, uf.Error,
                                (select max(p.Published) 
                                 from rss.Posts p 
                                 where p.FeedId = uf.FeedId) as LastPublished,
                                json_query((
                                    select p.PostId, p.Title, p.Published, p.Description, p.Link, p.Slug,
                                        p.IsRead, p.IsFavorite, p.Content, p.Author
                                    from rss.AppPosts p
                                    where 
                                        p.FeedId = uf.FeedId and
                                        p.Published >= @MinDate and p.Published <= @MaxDate and
                                        p.Slug = @PostSlug
                                    order by p.Published desc
                                    for json path
                                )) as Posts
                            from rss.AppFeeds uf
                            where uf.UserId = @UserId and uf.ChannelId = uc.Id and uf.Slug = @FeedSlug
                            for json path
                        )) as Feeds
                    from rss.UserChannels uc
                    where uc.UserId = @UserId and uc.Slug = @ChannelSlug
                    for json path;
                    """;
            }
        }
        else if (!string.IsNullOrWhiteSpace(feed))
        {
            this.Granularity = GranularityLevel.Feed;

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Description,
                            uf.Link, uf.Updated, uf.Scheduled, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished,
                            json_query((
                                select p.PostId, p.Title, p.Description, p.Published, p.Link, p.Slug,
                                    p.IsRead, p.IsFavorite, p.Author
                                from rss.AppPosts p
                                /**search-join**/
                                where p.FeedId = uf.FeedId
                                    /**filter-where-expr**/
                                order by p.Published desc
                                offset @SkipCount rows fetch next @TakeCount rows only
                                for json path
                            )) as Posts
                        from rss.AppFeeds uf
                        where uf.UserId = @UserId and uf.ChannelId = uc.Id and uf.Slug = @FeedSlug
                        for json path
                    )) as Feeds
                from rss.UserChannels uc
                where uc.UserId = @UserId and uc.Slug = @ChannelSlug
                for json path;
                """;
        }
        else if (!string.IsNullOrWhiteSpace(channel))
        {
            this.Granularity = GranularityLevel.Channel;
            page = new ChannelPageRequest(page);

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Description,
                            uf.Link, uf.Updated, uf.Scheduled, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished,
                             json_query((
                                select p.PostId, p.Title, p.Description, p.Published, p.Link, p.Slug,
                                    p.IsRead, p.IsFavorite, p.Author
                                from rss.AppPosts p
                                /**search-join**/
                                where p.FeedId = uf.FeedId
                                    /**filter-where-expr**/
                                order by p.Published desc
                                offset @SkipCount rows fetch next @TakeCount rows only
                                for json path
                            )) as Posts
                        from rss.AppFeeds uf
                        where uf.UserId = @UserId and uf.ChannelId = uc.Id
                        order by uf.Title
                        for json path
                    )) as Feeds
                from rss.UserChannels uc
                where uc.UserId = @UserId and uc.Slug = @ChannelSlug
                for json path;
                """;
        }
        else if (string.IsNullOrWhiteSpace(page.Q))
        {
            this.Granularity = GranularityLevel.Channels;

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Description, 
                            uf.Link, uf.Updated, uf.Scheduled, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished
                        from rss.AppFeeds uf
                        where uf.UserId = @UserId and uf.ChannelId = uc.Id
                        order by uf.Title
                        for json path
                    )) as Feeds
                from rss.UserChannels uc
                where uc.UserId = @UserId
                order by uc.Name
                for json path;
                """;
        }
        else
        {
            // global search
            this.Granularity = GranularityLevel.Search;
            return RedirectToPage("Search", new { q = page.Q });
        }

        if (!string.IsNullOrWhiteSpace(page.Q))
        {
            // feed search
            this.Granularity = GranularityLevel.Search;
            return RedirectToPage("Search", new { q = page.Q, channel, feed });
        }

        if (!string.IsNullOrWhiteSpace(page.F))
        {
            var f = (PostFilter)new DbEnum<PostFilter>(page.F);
            if (f != PostFilter.None)
            {
                sql = sql
                    .Replace("/**filter-where-expr**/",
                        $"""
                        and {(f == PostFilter.Unread ? "p.IsRead is null" : "p.IsFavorite = 1")}
                        """);
            }
        }

        var pageSize = page.GetPageSize(this.PageContext.HttpContext);
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        this.Channels = await cnn.QueryJsonAsync<IEnumerable<RssChannel>>(sql,
            new
            {
                this.UserId,
                ChannelSlug = channel.AsVarChar(100),
                FeedSlug = feed.AsVarChar(100),
                PostSlug = post.AsVarChar(100),
                MinDate = minDate,
                MaxDate = maxDate,
                ((IPage)page).SkipCount,
                TakeCount = pageSize,
                TopN = pageSize * 7,
            }) ?? [];

        if (this.Granularity == GranularityLevel.Post &&
            this.Channels.FirstOrDefault()?.Feeds.FirstOrDefault()?.Posts.FirstOrDefault() is RssPost originalPost)
        {
            // get similar posts
            this.SimilarPosts = await cnn.QueryAsync<RssPostRef>(
                """
                with SimilarPosts as
                (
                    select 
                        row_number() over (partition by p.PostId order by s.score desc) as Occurrence,
                        p.PostId, p.Title, p.Description, p.Author, p.Link, p.Published, p.IsRead, p.IsFavorite,
                        p.Slug, f.Slug as FeedSlug, f.Title as FeedTitle, ch.Slug as ChannelSlug, s.score as Score
                    from rss.AppPosts p
                    inner join SemanticSimilarityTable(rss.Posts, *, @PostId) s on p.PostId = s.matched_document_key
                    inner join rss.AppFeeds f on p.FeedId = f.FeedId
                    inner join rss.UserChannels ch on f.ChannelId = ch.Id
                    where ch.UserId = @UserId
                )
                select sp.*
                from SimilarPosts sp
                where sp.Occurrence = 1 and sp.Score > 0.5
                order by sp.Score desc;
                """, new { this.UserId, originalPost.PostId }
                ) ?? [];
        }

        return Page();
    }

    public enum GranularityLevel
    {
        None,
        // posts found in the search, no content
        Search,
        // the single post
        Post,
        // some posts in the feed with content
        Posts,
        // all posts in the feed, no content
        Feed,
        // all feeds in the channel with some posts
        Channel,
        // all channels and feeds in them
        Channels,
    }

    public enum PostFilter
    {
        None,
        A = None,
        Unread,
        U = Unread,
        Favorites,
        F = Favorites,
    }

    public record SinglePostModel(RssPost Post, IEnumerable<RssPostRef> SimilarPosts, RssFeedInfo Feed, RssChannelInfo Channel);
}
