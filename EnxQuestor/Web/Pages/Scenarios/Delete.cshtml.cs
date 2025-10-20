using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;

namespace Web.Pages.Scenarios;

[Authorize(Policy = Web.Authorization.CrudPolicies.Delete)]
public class DeleteModel : PageModel
{
    private readonly AppDbContext _db;
    public DeleteModel(AppDbContext db) => _db = db;

    [FromRoute] public Guid Id { get; set; }

    public string? Title { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var e = await _db.Scenarios.Where(s => s.Id == Id).Select(s => new { s.Title }).FirstOrDefaultAsync();
        if (e is null) return NotFound();
        Title = e.Title;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var e = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == Id);
        if (e is null) return NotFound();
        _db.Scenarios.Remove(e);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
