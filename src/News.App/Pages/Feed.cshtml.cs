namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Net;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using News.App.Data;

[Authorize]
public class FeedModel : PageModel
{
    private readonly UserManager<AppUser> userManager;
    private readonly DbDataSource db;
    private readonly ILogger<ImportUrlModel> log;
    private IEnumerable<RssChannelInfo> channels = Array.Empty<RssChannelInfo>();

    public FeedModel(UserManager<AppUser> userManager, DbDataSource db, ILogger<ImportUrlModel> log)
    {
        this.userManager = userManager;
        this.db = db;
        this.log = log;
        this.Input = new();
    }

    [BindProperty]
    public InputModel Input { get; init; }

    public SelectList Channels => new(this.channels, nameof(RssChannelInfo.ChannelId), nameof(RssChannelInfo.Name), this.Input.ChannelId);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var userIdStr = this.userManager.GetUserId(this.User);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            // user not logged in or whatever
            this.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        await using var cnn = await this.db.OpenConnectionAsync(this.HttpContext.RequestAborted);
        this.channels = await cnn.QueryAsync<RssChannelInfo>(
            """
            select uc.Id as ChannelId, uc.Name, uc.Slug
            from rss.UserChannels uc
            where uc.UserId = @userId
            order by uc.Name;
            """, new { userId });

        await base.OnPageHandlerExecutionAsync(context, next);
    }

    public void OnGet(Guid id)
    {
    }

    public record InputModel
    {
        public Guid FeedId { get; init; }

        [Required, Url, Display(Name = "Feed URL")]
        public string FeedUrl { get; init; } = "";

        [Display(Name = "Feed title")]
        public string? FeedTitle { get; init; }

        [Required, RegularExpression("^[a-z][a-z0-9-]*$"), MaxLength(50), Display(Name = "Feed slug")]
        public string FeedSlug { get; init; } = "";

        [Required, Display(Name = "Feed channel")]
        public Guid ChannelId { get; init; }
    }
}
