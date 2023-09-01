namespace News.App.Pages;

using System.Data.Common;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using News.App.Data;

public abstract class AppPageModel : PageModel
{
    private Guid? userId;

    protected Guid UserId => this.userId ??=
        Guid.TryParse(this.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
}

public abstract class EditPageModel : AppPageModel
{
    protected readonly DbDataSource db;

    protected EditPageModel(DbDataSource db)
    {
        this.db = db;
        this.Channels = new(Array.Empty<RssChannelInfo>(), nameof(RssChannelInfo.ChannelId), nameof(RssChannelInfo.Name));
    }

    public SelectList Channels { get; private set; }

    public sealed override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        await using var cnn = await this.db.OpenConnectionAsync(this.HttpContext.RequestAborted);
        var channels = await cnn.QueryAsync<RssChannelInfo>(
            """
            select uc.Id as ChannelId, uc.Name, uc.Slug
            from rss.UserChannels uc
            where uc.UserId = @UserId
            order by uc.Name;
            """, new { this.UserId });
        this.Channels = new(channels, nameof(RssChannelInfo.ChannelId), nameof(RssChannelInfo.Name));

        await base.OnPageHandlerExecutionAsync(context, next);
    }
}
