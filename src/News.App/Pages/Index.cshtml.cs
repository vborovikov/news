namespace News.App.Pages;

using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;
using Relay.InteractionModel;

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

    public IEnumerable<RssChannel> Channels { get; private set; } = Enumerable.Empty<RssChannel>();

    public async Task OnGet([FromQuery] PageRequest page, string? channel = null, string? feed = null,
        int year = 0, int month = 0, int day = 0, string? post = null,
        CancellationToken cancellationToken = default)
    {
        if (this.UserId == default)
        {
            // user not logged in
            return;
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
                                uf.Link, uf.Updated, uf.Error,
                                (select max(p.Published) 
                                 from rss.Posts p 
                                 where p.FeedId = uf.FeedId) as LastPublished,
                                json_query((
                                    select p.PostId, p.Title, p.Published, p.Description, p.Link, p.Slug, p.IsRead, p.Content, p.Author
                                    from rss.AppPosts p
                                    where p.FeedId = uf.FeedId and p.Published >= @MinDate and p.Published <= @MaxDate
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
                                uf.Link, uf.Updated, uf.Error,
                                (select max(p.Published) 
                                 from rss.Posts p 
                                 where p.FeedId = uf.FeedId) as LastPublished,
                                json_query((
                                    select p.PostId, p.Title, p.Published, p.Description, p.Link, p.Slug, p.IsRead, p.Content, p.Author
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
                            uf.Link, uf.Updated, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished,
                            json_query((
                                select p.PostId, p.Title, p.Description, p.Published, p.Link, p.Slug, p.IsRead, p.Author
                                from rss.AppPosts p
                                where p.FeedId = uf.FeedId
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

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Description,
                            uf.Link, uf.Updated, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished,
                             json_query((
                                select top 3 p.PostId, p.Title, p.Description, p.Published, p.Link, p.Slug, p.IsRead, p.Author
                                from rss.AppPosts p
                                where p.FeedId = uf.FeedId
                                order by p.Published desc
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
        else
        {
            this.Granularity = GranularityLevel.Channels;

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Description, 
                            uf.Link, uf.Updated, uf.Error,
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

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        this.Channels = await cnn.QueryJsonAsync<IEnumerable<RssChannel>>(sql,
            new
            {
                this.UserId,
                ChannelSlug = channel,
                FeedSlug = feed,
                PostSlug = post,
                MinDate = minDate,
                MaxDate = maxDate,
                ((IPage)page).SkipCount,
                ((IPage)page).TakeCount
            }) ?? [];
    }

    public enum GranularityLevel
    {
        None,
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
}
