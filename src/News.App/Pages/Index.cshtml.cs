namespace News.App.Pages;

using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Data;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class IndexModel : PageModel
{
    private readonly DbDataSource db;
    private readonly ILogger<IndexModel> log;

    public IndexModel(DbDataSource db, ILogger<IndexModel> log)
    {
        this.db = db;
        this.log = log;
    }

    public IEnumerable<RssChannel> Channels { get; private set; } = Enumerable.Empty<RssChannel>();

    public async Task OnGetAsync(string? slug = null, CancellationToken cancellationToken = default)
    {
        var userIdStr = this.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            // user not logged in
            return;
        }

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(slug))
        {
            // get user channels
            this.Channels = JsonSerializer.Deserialize<IEnumerable<RssChannel>>(await cnn.ExecuteScalarAsync<string>(
                """
                select (
                    select uc.ChannelId, uc.Name, uc.Slug,
                        json_query((
                           select uf.FeedId, uf.Title, uf.Slug
                           from rss.UserFeeds uf
                           where uf.UserId = @userId and uf.ChannelId = uc.ChannelId
                           for json path
                        )) as Feeds
                    from rss.UserChannels uc
                    where uc.UserId = @userId
                    order by uc.Name
                    for json path
                );
                """, new { userId }) ?? "[]") ?? Enumerable.Empty<RssChannel>();
        }
        else
        {
            // get user channel feeds
            this.Channels = JsonSerializer.Deserialize<IEnumerable<RssChannel>>(await cnn.ExecuteScalarAsync<string>(
                """
                select (
                    select uc.ChannelId, uc.Name, uc.Slug,
                        json_query((
                           select uf.FeedId, uf.Title, uf.Slug
                           from rss.UserFeeds uf
                           where uf.UserId = @userId and uf.ChannelId = uc.ChannelId
                           for json path
                        )) as Feeds
                    from rss.UserChannels uc
                    where uc.UserId = @userId and uc.Slug = @slug
                    order by uc.Name
                    for json path
                );
                """, new { userId, slug }) ?? "[]") ?? Enumerable.Empty<RssChannel>();
        }
    }
}
