using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace Infrastructure.Telegram;

public interface ITelegramWebhookHandler
{
    Task HandleAsync(Update update, CancellationToken ct);
}