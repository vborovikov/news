namespace News.App.Pages;

using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;
using Relay.InteractionModel;
using Spryer;
using XPage = Relay.InteractionModel.Page;

[Authorize]
public class SearchModel : AppPageModel
{
    private readonly DbDataSource db;
    private readonly ILogger<SearchModel> log;

    public SearchModel(DbDataSource db, ILogger<SearchModel> log)
    {
        this.db = db;
        this.log = log;
    }

    public IEnumerable<RssSearchPost> Posts { get; private set; } = [];

    public async Task<IActionResult> OnGet([FromQuery] PageRequest page, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(page.Q))
            return RedirectToPage("Index");

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);

        this.Posts = await cnn.QueryAsync<RssSearchPost>(
            """
            select
                p.PostId, p.Title, p.Description, p.Published, p.IsRead, p.IsFavorite, p.Author,
                p.Link, p.Slug, f.Slug as FeedSlug, f.Title as FeedTitle, c.Slug as ChannelSlug, c.Name as ChannelName
            from rss.AppPosts p
            inner join freetexttable(rss.Posts, *, @Search, @TopN) ft on ft.[Key] = p.PostId
            inner join rss.AppFeeds f on f.FeedId = p.FeedId
            inner join rss.UserChannels c on c.Id = f.ChannelId
            where f.UserId = @UserId
            order by ft.Rank desc, p.Published desc
            offset @SkipCount rows fetch next @TakeCount rows only;
            """, new
            {
                this.UserId,
                ((IPage)page).SkipCount,
                ((IPage)page).TakeCount,
                Search = page.Q.AsNVarChar(250),
                TopN = XPage.AvailablePageSizes[^1] * 7,
            }) ?? [];

        return Page();
    }

    public record RssSearchPost : RssPostInfo
    {
        public required string ChannelSlug { get; init; }
        public required string ChannelName { get; init; }
        public required string FeedSlug { get; init; }
        public required string FeedTitle { get; init; }
    }
}
