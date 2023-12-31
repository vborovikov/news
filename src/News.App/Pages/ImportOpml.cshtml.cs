namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using News.App.Data;

[Authorize]
public class ImportOpmlModel : PageModel
{
    private readonly AppOptions options;
    private readonly UserManager<AppUser> userManager;
    private readonly ILogger<ImportOpmlModel> log;

    public ImportOpmlModel(UserManager<AppUser> userManager, IOptions<AppOptions> options, ILogger<ImportOpmlModel> log)
    {
        this.userManager = userManager;
        this.options = options.Value;
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
        if (!this.ModelState.IsValid || !this.Input.IsValid)
        {
            this.ModelState.AddModelError("", "OPML file is empty or invalid");
            return Page();
        }

        if (!this.options.OpmlDirectory.Exists)
        {
            this.ModelState.AddModelError("", "OPML directory does not exist");
            this.log.LogWarning("OPML directory '{opmlDirectory}' does not exist", this.options.OpmlDirectory);
            return Page();
        }

        var userIdStr = this.userManager.GetUserId(this.User);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            // user not logged in
            this.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            this.ModelState.AddModelError("", "User id is invalid");
            return Page();
        }

        var opmlFileNumber = 0;
        var opmlFile = new FileInfo(Path.Combine(this.options.OpmlDirectory.FullName, $"{userId}-{opmlFileNumber}.opml"));
        while (opmlFile.Exists)
        {
            if (opmlFileNumber == 100)
                break;
            opmlFile = new FileInfo(Path.Combine(this.options.OpmlDirectory.FullName, $"{userId}-{opmlFileNumber++}.opml"));
        }
        if (opmlFile.Exists)
        {
            this.ModelState.AddModelError("", "Too many OPML files pending import");
            this.log.LogWarning("Too many OPML files pending import");
            return Page();
        }

        try
        {
            await using var opmlFileStream = opmlFile.OpenWrite();
            await this.Input.OpmlFile.CopyToAsync(opmlFileStream, cancellationToken);
        }
        catch (Exception x)
        {
            this.log.LogError(x, "Error importing opml file '{fileName}'", opmlFile.Name);
            this.ModelState.AddModelError("", "Error importing OPML file");
            this.ModelState.AddModelError("", x.Message);
            return Page();
        }

        return RedirectToPage("Index");
    }

    public record InputModel
    {
        [Required, Display(Name = "OPML file")]
        public IFormFile? OpmlFile { get; init; }

        [MemberNotNullWhen(true, nameof(OpmlFile))]
        public bool IsValid => this.OpmlFile is not null && this.OpmlFile.Length > 0;
    }
}
