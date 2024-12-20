namespace News.App.Pages;

using System.ComponentModel;
using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;
using News.App.Pages.Shared;
using Relay.InteractionModel;
using Spryer;

[Authorize]
public class SearchModel : AppPageModel
{
    public const string PageSizeCookieName = "searchPageSize";

    private readonly DbDataSource db;
    private readonly ILogger<SearchModel> log;

    public SearchModel(DbDataSource db, ILogger<SearchModel> log)
    {
        this.db = db;
        this.log = log;
    }

    public IEnumerable<RssSearchPost> Posts { get; private set; } = [];

    public async Task<IActionResult> OnGet([FromQuery] PageRequest page, string? channel = null, string? feed = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(page.Q))
            return RedirectToPage("Index");

        var sql =
            """
            select
                p.PostId, p.Title, p.Description, p.Published, p.IsRead, p.IsFavorite, p.Author,
                p.Link, p.Slug, f.Slug as FeedSlug, f.Title as FeedTitle, c.Slug as ChannelSlug, c.Name as ChannelName
            from rss.AppPosts p
            inner join freetexttable(rss.Posts, *, @Search, @TopN) ft on ft.[Key] = p.PostId
            inner join rss.AppFeeds f on f.FeedId = p.FeedId
            inner join rss.UserChannels c on c.Id = f.ChannelId
            where f.UserId = @UserId
                /**where-expr**/
            order by /**order-by-expr**/
            offset @SkipCount rows fetch next @TakeCount rows only;
            """;

        if (!string.IsNullOrWhiteSpace(feed))
        {
            sql = sql.Replace("/**where-expr**/", "and f.Slug = @FeedSlug and c.Slug = @ChannelSlug");
        }
        else if (!string.IsNullOrWhiteSpace(channel))
        {
            sql = sql.Replace("/**where-expr**/", "and c.Slug = @ChannelSlug");
        }

        var f = (SearchFilter)new DbEnum<SearchFilter>(page.F);
        if (f == SearchFilter.Relevant)
        {
            sql = sql.Replace("/**order-by-expr**/", "ft.Rank desc, p.IsFavorite desc, p.IsRead desc, p.Published desc");
        }
        else
        {
            sql = sql.Replace("/**order-by-expr**/", "p.Published desc, ft.Rank desc, p.IsFavorite desc, p.IsRead desc");
        }

        var pageSize = page.GetPageSize(this.PageContext.HttpContext, PageSizeCookieName);
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);

        this.Posts = await cnn.QueryAsync<RssSearchPost>(sql, new
        {
            this.UserId,
            ((IPage)page).SkipCount,
            TakeCount = pageSize,
            Search = page.Q.AsNVarChar(250),
            TopN = pageSize * 7,
            ChannelSlug = channel.AsVarChar(100),
            FeedSlug = feed.AsVarChar(100),
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

    public enum SearchFilter
    {
        [AmbientValue("R")]
        Relevant,
        [AmbientValue("T")]
        Recent,
    }
}
