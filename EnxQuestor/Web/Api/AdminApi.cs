using System;
using System.Collections.Generic;
using System.Threading;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Web.Api;

public static class AdminApi
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").RequireAuthorization("TeacherOnly");
        g.MapPost("/scenarios", async (AppDbContext db, Domain.Scenarios.Scenario s, CancellationToken ct) =>
        {
            db.Scenarios.Add(s);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/scenarios/{s.Id}", s.Id);
        });
        g.MapPut("/scenarios/{id:guid}",
            async (AppDbContext db, Guid id, Domain.Scenarios.Scenario payload, CancellationToken ct) =>
            {
                var s = await db.Scenarios.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (s == null) return Results.NotFound();
                s.Publish(payload.IsPublished);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            });
        g.MapPost("/scenarios/{id:guid}/steps",
            async (AppDbContext db, Guid id, List<Domain.Scenarios.ScenarioStep> steps, CancellationToken ct) =>
            {
                var s = await db.Scenarios.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id, ct);
                if (s == null) return Results.NotFound();
                s.SetSteps(steps);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            });
        g.MapPost("/achievements", async (AppDbContext db, Domain.Achievements.Achievement a, CancellationToken ct) =>
        {
            db.Achievements.Add(a);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/admin/achievements/{a.Id}", a.Id);
        });
    }
}