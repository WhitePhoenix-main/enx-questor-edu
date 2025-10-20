using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;

namespace Web.Pages.Scenarios;

[Authorize(Policy = Web.Authorization.CrudPolicies.Create)]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required, MinLength(3)]
        public string Title { get; set; } = default!;

        [Required, RegularExpression("^[a-z0-9-]+$")]
        public string Slug { get; set; } = default!;

        public string? Description { get; set; }
        public string? Tags { get; set; }

        [Range(1, 5)]
        public int Difficulty { get; set; } = 1;

        [Display(Name = "Опубликован")]
        public bool IsPublished { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        // уникальность slug
        if (await _db.Scenarios.AnyAsync(s => s.Slug == Input.Slug))
        {
            ModelState.AddModelError("Input.Slug", "Такой slug уже используется.");
            return Page();
        }

        var authorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(authorId))
            return Challenge(); // требует входа

        var e = Domain.Scenarios.Scenario.Create(
            Input.Title,
            Input.Slug,
            authorId,
            Input.Difficulty,
            Input.Description ?? string.Empty,
            Input.Tags ?? string.Empty);

        e.Publish(Input.IsPublished);

        _db.Scenarios.Add(e);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
