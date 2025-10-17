
using System;
using System.Threading;
using Infrastructure.Telegram;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Telegram.Bot.Types;
namespace Web.Bot;
public static class WebhookEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/bot/webhook/{secret}", async ([FromRoute] string secret, [FromServices] IConfiguration cfg, [FromServices] ITelegramWebhookHandler handler, [FromBody] Update update, CancellationToken ct) =>
        {
            var expected = cfg["Telegram:WebhookSecret"];
            if (!string.Equals(secret, expected, StringComparison.Ordinal))
                return Results.Unauthorized();
            await handler.HandleAsync(update, ct);
            return Results.Ok();
        });
    }
}
