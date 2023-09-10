namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;

[Authorize]
public class ExportModel : AppPageModel
{
    private readonly DbDataSource db;
    private readonly ILogger<ExportModel> log;

    public ExportModel(DbDataSource db, ILogger<ExportModel> log)
    {
        this.db = db;
        this.log = log;
        this.Input = new();
    }

    [BindProperty]
    public InputModel Input { get; init; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid || this.Input.Format != FeedExportFormat.Opml)
        {
            this.ModelState.AddModelError("", "Format not supported yet");
            return Page();
        }

        try
        {
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
                        where uc.Id = uf.ChannelId and (@IncludeBroken = 1 or f.Error is null)
                        order by [title]
                        for xml raw('outline'), type)
                    from rss.UserChannels uc
                    where uc.UserId = u.Id
                    order by uc.Name
                    for xml raw('outline'), type) as [body]
                from asp.Users u
                where u.Id = @UserId
                for xml raw('opml'), type;
                """, new { this.UserId, this.Input.IncludeBroken });

            return File(Encoding.UTF8.GetBytes(xml), "text/x-opml", "feeds.opml");
        }
        catch (Exception x)
        {
            this.log.LogError(x, "Error exporting feeds");
            this.ModelState.AddModelError("", x.Message);
            return Page();
        }
    }

    public enum FeedExportFormat
    {
        [Display(Name = "OPML")]
        Opml,
        [Display(Name = "JSON")]
        Json,
        [Display(Name = "Text")]
        Text,
    }

    public record InputModel
    {
        [Required(AllowEmptyStrings = false), Display(Name = "Format")]
        public FeedExportFormat Format { get; init; }

        [Display(Name = "Include Broken")]
        public bool IncludeBroken { get; init; }
    }
}
