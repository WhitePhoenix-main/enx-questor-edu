using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

public sealed class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public List<Domain.Scenarios.Scenario> Scenarios { get; private set; } = new();
    public IndexModel(AppDbContext db) => _db = db;

    public async Task OnGet(string? tag, int? difficulty)
    {
        var q = _db.Scenarios.AsQueryable().Where(x => x.IsPublished);
        if (!string.IsNullOrWhiteSpace(tag)) q = q.Where(x => x.Tags.Contains(tag));
        if (difficulty.HasValue) q = q.Where(x => x.Difficulty == difficulty);
        Scenarios = await q.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }
}