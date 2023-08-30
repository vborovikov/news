namespace News.App.Pages;

using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using News.App.Data;

[Authorize]
public class ExportModel : PageModel
{
    private readonly UserManager<AppUser> userManager;
    private readonly DbDataSource db;
    private readonly ILogger<ExportModel> log;

    public ExportModel(UserManager<AppUser> userManager, DbDataSource db, ILogger<ExportModel> log)
    {
        this.userManager = userManager;
        this.db = db;
        this.log = log;
    }

    public async Task<IActionResult> OnGetAsync(string? format = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return Page();
        }
        var userIdStr = this.userManager.GetUserId(this.User);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            return Forbid();
        }

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        // query channels and feeds in XML format
        var xml = await cnn.QueryTextAsync(
            """
            select
            	(select ui.UserName as [title]
            	 from asp.Users ui
            	 where ui.Id = u.Id
            	 for xml raw('head'), type, elements),
            	(select uc.Name as [title], uc.Slug as [text], 
            		(select isnull(uf.Title, f.Title) as [title], uf.Slug as [text], f.Source as [xmlUrl], f.Link as [htmlUrl]
            		 from rss.UserFeeds uf
            		 inner join rss.Feeds f on uf.FeedId = f.Id
            		 where uc.Id = uf.ChannelId
            		 for xml raw('outline'), type)
            	from rss.UserChannels uc
            	where uc.UserId = u.Id
            	for xml raw('outline'), type) as [body]
            from asp.Users u
            where u.Id = @UserId
            for xml raw('opml'), type;
            """, new { UserId = userId });

        return File(Encoding.UTF8.GetBytes(xml), "text/x-opml", "feeds.opml");
    }
}
