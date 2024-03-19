namespace News.App.Pages;

using System.Data.Common;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;

[Authorize]
public class DailyModel : AppPageModel
{
    public const string DateFormat = "yyyy-MM-dd";

    private readonly DbDataSource db;
    private readonly ILogger<DailyModel> log;

    public DailyModel(DbDataSource db, ILogger<DailyModel> log)
    {
        this.db = db;
        this.log = log;
    }

    public DateTime Date { get; private set; } = DateTime.Today;

    public IEnumerable<ChannelSummary> Channels { get; private set; } = [];

    public async Task OnGet([FromQuery] string? day = null, string? channel = null, CancellationToken cancellationToken = default)
    {
        var maxPublished = DateTimeOffset.Now;
        var minPublished = maxPublished.AddDays(-1);
        if (day is string dateStr &&
            DateTime.TryParseExact(dateStr, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) &&
            date < DateTime.Today)
        {
            minPublished = new DateTimeOffset(date.Date);
            maxPublished = new DateTimeOffset(date.Date.Add(new TimeSpan(23, 59, 59)));
        }
        this.Date = maxPublished.Date;

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(channel))
        {
            this.Channels = await cnn.QueryJsonAsync<IEnumerable<ChannelSummary>>(
                """
            select uc.Id as ChannelId, uc.Name, uc.Slug,
                json_query((
                    select rp.*
                    from (
                        select 
                            ap.PostId, ap.Title, ap.Published, ap.Description, 
                            ap.Link, ap.Slug, ap.Author, ap.IsRead,
                            af.Slug as FeedSlug, af.Title as FeedTitle,
                            row_number() over (partition by af.FeedId order by ap.Published desc) as PostNumber
                        from rss.AppFeeds af
                        inner join rss.AppPosts ap on af.FeedId = ap.FeedId
                        where af.UserId = @UserId and af.ChannelId = uc.Id and
                            ap.Published >= @MinPublished and ap.Published <= @MaxPublished
                    ) rp
                    where rp.PostNumber <= 3
                    for json path
                )) as Posts
            from rss.UserChannels uc
            where uc.UserId = @UserId
            order by uc.Name
            for json path;
            """,
                new
                {
                    this.UserId,
                    MinPublished = minPublished,
                    MaxPublished = maxPublished,
                }) ?? [];
        }
        else
        {

        }
    }

    public record PostSummary : PostBase
    {
        public required string FeedSlug { get; init; }
        public required string FeedTitle { get; init; }
    }

    public record ChannelSummary : ChannelBase
    {
        public IEnumerable<PostSummary> Posts { get; init; } = Enumerable.Empty<PostSummary>();
    }
}
