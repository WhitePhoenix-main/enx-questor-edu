using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Infrastructure.Telegram;

public interface ITelegramWebhookHandler
{
    Task HandleAsync(Update update, CancellationToken ct);
}

public sealed class TelegramWebhookHandler : ITelegramWebhookHandler
{
    private readonly AppDbContext _db;
    private readonly ITelegramBotClient _bot;

    public TelegramWebhookHandler(AppDbContext db, ITelegramBotClient bot)
    {
        _db = db;
        _bot = bot;
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        if (await _db.BotUpdateLogs.AnyAsync(x => x.UpdateId == update.Id, ct)) return;
        _db.BotUpdateLogs.Add(Domain.Telegram.BotUpdateLog.Create(update.Id, update.Type.ToString(),
            JsonSerializer.Serialize(update)));
        await _db.SaveChangesAsync(ct);
        try
        {
            if (update.Type == UpdateType.Message && update.Message is { } msg)
            {
                var text = msg.Text ?? "";
                if (text.StartsWith("/start"))
                {
                    await _bot.SendTextMessageAsync(msg.Chat,
                        "Привет! Для привязки аккаунта зайдите на сайт в профиль и нажмите «Связать Telegram». Затем пришлите боту одноразовый код.",
                        cancellationToken: ct);
                }
                else if (text.StartsWith("/me"))
                {
                    var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == msg.From!.Id.ToString(), ct);
                    if (user == null)
                    {
                        await _bot.SendTextMessageAsync(msg.Chat,
                            "Аккаунт не привязан. Отправьте одноразовый код привязки.", cancellationToken: ct);
                    }
                    else
                    {
                        var total = await _db.Attempts.CountAsync(a => a.UserId == user.Id, ct);
                        var completed = await _db.Attempts.CountAsync(
                            a => a.UserId == user.Id && a.Status == Domain.Attempts.AttemptStatus.Completed, ct);
                        var badges = await _db.UserAchievements.CountAsync(x => x.UserId == user.Id, ct);
                        await _bot.SendTextMessageAsync(msg.Chat,
                            $"Прогресс:\nПопыток: {total}\nЗавершено: {completed}\nДостижений: {badges}",
                            cancellationToken: ct);
                    }
                }
                else
                {
                    var link = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.OneTimeCode == text, ct);
                    if (link != null && link.TryConsume())
                    {
                        var user = await _db.Users.FirstAsync(u => u.Id == link.UserId, ct);
                        user.TelegramId = msg.From!.Id.ToString();
                        user.TelegramUsername = msg.From!.Username;
                        await _db.SaveChangesAsync(ct);
                        await _bot.SendTextMessageAsync(msg.Chat, "Аккаунт успешно привязан ✅", cancellationToken: ct);
                    }
                }
            }
        }
        finally
        {
            var log = await _db.BotUpdateLogs.FirstAsync(x => x.UpdateId == update.Id, ct);
            log.MarkProcessed();
            await _db.SaveChangesAsync(ct);
        }
    }
}