namespace News.App.Pages;

using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using News.App.Data;

public class IndexModel : PageModel
{
    private readonly UserManager<AppUser> userManager;
    private readonly DbDataSource db;
    private readonly ILogger<IndexModel> log;

    public IndexModel(UserManager<AppUser> userManager, DbDataSource db, ILogger<IndexModel> log)
    {
        this.userManager = userManager;
        this.db = db;
        this.log = log;
    }

    public GranularityLevel Granularity { get; private set; } = GranularityLevel.None;

    public IEnumerable<RssChannel> Channels { get; private set; } = Enumerable.Empty<RssChannel>();

    public async Task OnGetAsync(string? channel = null, string? feed = null,
        int year = 0, int month = 0, int day = 0, int? hour = null, int? minute = null,
        CancellationToken cancellationToken = default)
    {
        var userIdStr = this.userManager.GetUserId(this.User);
        if (!Guid.TryParse(userIdStr, out var userId))
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
            this.Granularity = GranularityLevel.Posts;

            // tweaking dates when showing posts
            if (minute is not null)
            {
                // one minute (super-noisy feeds)
                minDate = new DateTimeOffset(year, month, day, hour ?? 0, minute.Value, 0, minDate.Offset);
                maxDate = minDate.AddMinutes(1);
            }
            else if (hour is not null)
            {
                // one hour (noisy feeds)
                minDate = new DateTimeOffset(year, month, day, hour.Value, minute: 0, second: 0, minDate.Offset);
                maxDate = minDate.AddHours(1);
            }
            else if (day > 0)
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

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Updated, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished,
                            json_query((
                                select p.PostId, p.Title, p.Published, p.Description, p.Link, p.IsRead, p.Content
                                from rss.AppPosts p
                                where p.FeedId = uf.FeedId and p.Published >= @MinDate and p.Published <= @MaxDate
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
        else if (!string.IsNullOrWhiteSpace(feed))
        {
            this.Granularity = GranularityLevel.Feed;

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Updated, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished,
                            json_query((
                                select p.PostId, p.Title, p.Published, p.Link, p.IsRead
                                from rss.AppPosts p
                                where p.FeedId = uf.FeedId
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
        else if (!string.IsNullOrWhiteSpace(channel))
        {
            this.Granularity = GranularityLevel.Channel;

            sql =
                """
                select uc.Id as ChannelId, uc.Name, uc.Slug,
                    json_query((
                        select uf.FeedId, uf.Title, uf.Slug, uf.Updated, uf.Error,
                            (select max(p.Published) 
                             from rss.Posts p 
                             where p.FeedId = uf.FeedId) as LastPublished
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
                        select uf.FeedId, uf.Title, uf.Slug, uf.Updated, uf.Error,
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
                UserId = userId,
                ChannelSlug = channel,
                FeedSlug = feed,
                MinDate = minDate,
                MaxDate = maxDate
            }) ?? Enumerable.Empty<RssChannel>();
    }

    public enum GranularityLevel
    {
        None,
        // some posts in the feed with content
        Posts,
        // all posts in the feed, no content
        Feed,
        // all feeds in the channel
        Channel,
        // all channels and feeds in them
        Channels,
    }
}
