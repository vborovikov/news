namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

[BindProperties]
public class ImportOpmlModel : PageModel
{
    [Required]
    public IFormFile OpmlFile { get; set; }

    public async Task OnGetAsync()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (this.OpmlFile is null || this.OpmlFile.Length == 0)
        {
            this.ModelState.AddModelError(nameof(this.OpmlFile), "File is empty");
            return Page();
        }

        return RedirectToPage("Index");
    }
}
