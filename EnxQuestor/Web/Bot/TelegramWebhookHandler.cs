using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure.Persistence;
using Infrastructure.Telegram;                 // интерфейс ITelegramWebhookHandler
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Web.Bot;
using Web.Identity;                           // ApplicationUser

namespace Web.Telegram;

public sealed class TelegramWebhookHandler : ITelegramWebhookHandler
{
    private readonly AppDbContext _db;                 // доменная БД (без AspNetUsers)
    private readonly UserManager<ApplicationUser> _um; // Identity
    private readonly ITelegramBotClient _bot;

    public TelegramWebhookHandler(
        AppDbContext db,
        UserManager<ApplicationUser> um,
        ITelegramBotClient bot)
    {
        _db = db;
        _um = um;
        _bot = bot;
    }

    public async Task HandleAsync(Update update, CancellationToken ct)
    {
        // идемпотентный лог апдейтов
        if (await _db.BotUpdateLogs.AnyAsync(x => x.UpdateId == update.Id, ct)) return;
        _db.BotUpdateLogs.Add(Domain.Telegram.BotUpdateLog.Create(
            update.Id, update.Type.ToString(), JsonSerializer.Serialize(update)));
        await _db.SaveChangesAsync(ct);

        try
        {
            if (update.Type != UpdateType.Message || update.Message is not { } msg) return;

            var text = msg.Text ?? string.Empty;

            if (text.StartsWith("/start"))
            {
                await _bot.SendTextMessageAsync(
                    msg.Chat,
                    "Привет! В профиле на сайте нажмите «Связать Telegram», затем пришлите сюда одноразовый код.",
                    cancellationToken: ct);
                return;
            }

            if (text.StartsWith("/me"))
            {
                var tgId = msg.From?.Id.ToString();
                if (string.IsNullOrEmpty(tgId))
                {
                    await _bot.SendTextMessageAsync(msg.Chat, "Не удалось определить ваш Telegram ID.", cancellationToken: ct);
                    return;
                }

                var user = await _um.Users.FirstOrDefaultAsync(u => u.TelegramId == tgId, ct);
                if (user is null)
                {
                    await _bot.SendTextMessageAsync(msg.Chat, "Аккаунт не привязан. Отправьте одноразовый код.", cancellationToken: ct);
                    return;
                }

                var total = await _db.Attempts.CountAsync(a => a.UserId == user.Id, ct);
                var completed = await _db.Attempts.CountAsync(
                    a => a.UserId == user.Id && a.Status == Domain.Attempts.AttemptStatus.Completed, ct);
                var badges = await _db.UserAchievements.CountAsync(x => x.UserId == user.Id, ct);

                await _bot.SendTextMessageAsync(
                    msg.Chat, $"Прогресс:\nПопыток: {total}\nЗавершено: {completed}\nДостижений: {badges}",
                    cancellationToken: ct);
                return;
            }

            // иначе считаем, что это одноразовый код привязки
            var code = text.Trim();
            var link = await _db.TelegramLinks.FirstOrDefaultAsync(l => l.OneTimeCode == code, ct);
            if (link is null || !link.TryConsume()) return;

            var userById = await _um.FindByIdAsync(link.UserId);
            if (userById is null)
            {
                await _bot.SendTextMessageAsync(msg.Chat, "Пользователь не найден.", cancellationToken: ct);
                return;
            }

            userById.TelegramId = msg.From?.Id.ToString();
            userById.TelegramUsername = msg.From?.Username;

            var res = await _um.UpdateAsync(userById);
            if (!res.Succeeded)
            {
                await _bot.SendTextMessageAsync(
                    msg.Chat, "Не удалось привязать Telegram к аккаунту.",
                    cancellationToken: ct);
                return;
            }

            await _db.SaveChangesAsync(ct); // сохранить consumption для ссылки
            await _bot.SendTextMessageAsync(msg.Chat, "Аккаунт успешно привязан ✅", cancellationToken: ct);
        }
        finally
        {
            var log = await _db.BotUpdateLogs.FirstAsync(x => x.UpdateId == update.Id, ct);
            log.MarkProcessed();
            await _db.SaveChangesAsync(ct);
        }
    }
}
