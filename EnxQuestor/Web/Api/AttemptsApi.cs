using System;
using Application.Abstractions;
using Application.DTO;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Web.Api;

public static class AttemptsApi
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/attempts").RequireAuthorization();
        g.MapPost("/",
            async (IAttemptService svc, StartAttemptRequest req, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
                return Results.Ok(await svc.StartAsync(uid, req, ct));
            });
        g.MapPost("/{id:guid}/answer",
            async (IAttemptService svc, Guid id, AnswerRequest req, ClaimsPrincipal user, CancellationToken ct) =>
            {
                req = req with { AttemptId = id };
                if (req.StepId == Guid.Empty || string.IsNullOrWhiteSpace(req.AnswerJson) ||
                    req.AnswerJson.Length > 4000)
                    return Results.BadRequest(new { error = "Invalid payload" });
                var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
                return Results.Ok(await svc.AnswerAsync(uid, req, ct));
            });
        g.MapPost("/{id:guid}/finish",
            async (IAttemptService svc, Guid id, ClaimsPrincipal user, CancellationToken ct) =>
            {
                var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
                return Results.Ok(await svc.FinishAsync(uid, new FinishAttemptRequest(id), ct));
            });
        g.MapGet("/{id:guid}", async (IAttemptService svc, Guid id, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            return Results.Ok(await svc.GetAsync(uid, id, ct));
        });
    }
}