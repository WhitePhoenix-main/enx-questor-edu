using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;

namespace Web.Pages.Scenarios;

[Authorize(Policy = Web.Authorization.CrudPolicies.Update)]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    public EditModel(AppDbContext db) => _db = db;

    [FromRoute] public Guid Id { get; set; }

    [BindProperty] public InputModel Input { get; set; } = new();

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

    public async Task<IActionResult> OnGetAsync()
    {
        var e = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == Id);
        if (e is null) return NotFound();

        Input = new InputModel
        {
            Title = e.Title,
            Slug = e.Slug,
            Description = e.Description,
            Tags = e.Tags,
            Difficulty = e.Difficulty,
            IsPublished = e.IsPublished
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var e = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == Id);
        if (e is null) return NotFound();

        // Проверка уникальности slug среди других записей
        var slugBusy = await _db.Scenarios.AnyAsync(x => x.Slug == Input.Slug && x.Id != Id);
        if (slugBusy)
        {
            ModelState.AddModelError("Input.Slug", "Такой slug уже используется.");
            return Page();
        }

        // Домейн-апдейт вместо прямых присваиваний приватных сеттеров
        e.Update(
            Input.Title,
            Input.Slug,
            Input.Difficulty,
            Input.Description ?? string.Empty,
            Input.Tags ?? string.Empty,
            Input.IsPublished
        );

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
