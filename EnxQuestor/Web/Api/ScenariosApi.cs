
using System;
using System.Linq;
using System.Threading;
using Application.DTO;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
namespace Web.Api;
public static class ScenariosApi
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/scenarios");
        g.MapGet("/", async (AppDbContext db, string? tag, int? difficulty, string? authorId, CancellationToken ct) =>
        {
            var q = db.Scenarios.AsQueryable().Where(x=>x.IsPublished);
            if (!string.IsNullOrWhiteSpace(tag)) q = q.Where(x => x.Tags.Contains(tag));
            if (difficulty.HasValue) q = q.Where(x => x.Difficulty == difficulty);
            if (!string.IsNullOrWhiteSpace(authorId)) q = q.Where(x => x.AuthorId == authorId);
            var list = await q.OrderByDescending(x=>x.CreatedAt).ToListAsync(ct);
            var dto = list.Select(s => new ScenarioListItemDto(s.Id, s.Title, s.Slug, s.Tags, s.Difficulty, s.IsPublished)).ToList();
            return Results.Ok(dto);
        });
        g.MapGet("/{id:guid}", async (AppDbContext db, Guid id, CancellationToken ct) =>
        {
            var s = await db.Scenarios.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id && x.IsPublished, ct);
            if (s is null) return Results.NotFound();
            var dto = new ScenarioDetailsDto(s.Id, s.Title, s.Slug, s.Description, s.Tags, s.Difficulty, s.IsPublished,
                s.Steps.OrderBy(st=>st.Order).Select(st => new ScenarioStepDto(st.Id, st.Order, st.Title, st.StepType.ToString(), st.Content, st.MaxScore)).ToList());
            return Results.Ok(dto);
        });
    }
}
