using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;
using Web.Authorization;

namespace Web.Pages.Scenarios;

public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IAuthorizationService _auth;

    public IndexModel(AppDbContext db, IAuthorizationService auth)
    {
        _db = db;
        _auth = auth;
    }

    public sealed record Row(Guid Id, string Title, string Slug, bool IsPublished, int Difficulty);

    public List<Row> Items { get; private set; } = new();
    [Display(Name = "Поиск")]
    public string? Q { get; private set; }

    public bool CanCreate { get; private set; }
    public bool CanUpdate { get; private set; }
    public bool CanDelete { get; private set; }

    public async Task OnGetAsync(string? q)
    {
        Q = q;

        var query = _db.Scenarios.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Title, like) ||
                EF.Functions.ILike(s.Description, like) ||
                EF.Functions.ILike(s.Tags, like));
        }

        Items = await query
            .OrderByDescending(s => s.IsPublished)
            .ThenBy(s => s.Title)
            .Select(s => new Row(s.Id, s.Title, s.Slug, s.IsPublished, s.Difficulty))
            .ToListAsync();

        CanCreate = (await _auth.AuthorizeAsync(User, null, CrudPolicies.Create)).Succeeded;
        CanUpdate = (await _auth.AuthorizeAsync(User, null, CrudPolicies.Update)).Succeeded;
        CanDelete = (await _auth.AuthorizeAsync(User, null, CrudPolicies.Delete)).Succeeded;
    }
}