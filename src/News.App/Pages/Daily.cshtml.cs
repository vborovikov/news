namespace News.App.Pages;

using System.Data.Common;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;
using Relay.InteractionModel;
using Spryer;

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

    public async Task<IActionResult> OnGet([FromQuery] PageRequest page, [FromQuery] string? day = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(page.Q))
        {
            return RedirectToPage("Search", new { q = page.Q });
        }

        var maxPublished = DateTimeOffset.Now;
        var minPublished = maxPublished.AddDays(-1);
        if (day is string dateStr &&
            DateTime.TryParseExact(dateStr, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) &&
            date < DateTime.Today)
        {
            minPublished = new DateTimeOffset(date.Date);
            maxPublished = new DateTimeOffset(date.Date.AddDays(1).AddSeconds(-1)); // -1 second to escape the current date
        }
        this.Date = maxPublished.Date;

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        this.Channels = await cnn.QueryJsonAsync<IEnumerable<ChannelSummary>>(
            """
            select uc.Id as ChannelId, uc.Name, uc.Slug,
                json_query((
                    select rp.*
                    from (
                        select
                            ap.PostId, ap.Title, ap.Published, ap.Description, 
                            ap.Link, ap.Slug, ap.Author, ap.IsRead, ap.IsFavorite,
                            af.Slug as FeedSlug, af.Title as FeedTitle,
                            row_number() over (partition by af.FeedId order by ap.Published desc) as PostNumber
                        from rss.AppFeeds af
                        inner join rss.AppPosts ap on af.FeedId = ap.FeedId
                        where af.UserId = @UserId and af.ChannelId = uc.Id and
                            ap.Published >= @MinPublished and ap.Published <= @MaxPublished
                    ) rp
                    where rp.PostNumber <= 3
                    order by rp.FeedTitle, rp.Published desc
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

        return Page();
    }

    public record PostSummary : RssPostInfo
    {
        public required string FeedSlug { get; init; }
        public required string FeedTitle { get; init; }
    }

    public record ChannelSummary : RssChannelInfo
    {
        public IEnumerable<PostSummary> Posts { get; init; } = [];
    }
}
