namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

[Authorize]
public class ImportOpmlModel : PageModel
{
    public ImportOpmlModel()
    {
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

        return RedirectToPage("Index");
    }

    public record InputModel
    {
        [Required, Display(Name = "OPML file")]
        public IFormFile? OpmlFile { get; init; }

        public bool IsValid => this.OpmlFile is not null && this.OpmlFile.Length > 0;
    }
}
