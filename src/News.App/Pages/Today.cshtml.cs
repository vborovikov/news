namespace News.App.Pages;

using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using News.App.Data;

[Authorize]
public class TodayModel : AppPageModel
{
    private readonly DbDataSource db;
    private readonly ILogger<TodayModel> log;

    public TodayModel(DbDataSource db, ILogger<TodayModel> log)
    {
        this.db = db;
        this.log = log;
    }

    public IEnumerable<ChannelSummary> Channels { get; private set; } = Enumerable.Empty<ChannelSummary>();

    public async Task OnGet(CancellationToken cancellationToken = default)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        this.Channels = await cnn.QueryJsonAsync<IEnumerable<ChannelSummary>>(
            """
            select uc.Id as ChannelId, uc.Name, uc.Slug,
                json_query((
                    select ap.PostId, ap.Title, ap.Published, ap.Description, 
                        ap.Link, ap.Slug, af.Slug as FeedSlug,
                        ap.Author, ap.IsRead
                    from rss.AppFeeds af
                    inner join rss.AppPosts ap on af.FeedId = ap.FeedId
                    where af.UserId = @UserId and af.ChannelId = uc.Id and ap.Published >= @MinPublished
                    order by ap.Published desc
                    for json path
                )) as Posts
            from rss.UserChannels uc
            where uc.UserId = @UserId
            order by uc.Name
            for json path;
            """,
            new { this.UserId, MinPublished = DateTimeOffset.Now.AddDays(-1) }) ?? Enumerable.Empty<ChannelSummary>();
    }

    public record PostSummary : PostBase
    {
        public required string FeedSlug { get; init; }
    }

    public record ChannelSummary : ChannelBase
    {
        public IEnumerable<PostSummary> Posts { get; init; } = Enumerable.Empty<PostSummary>();
    }
}
