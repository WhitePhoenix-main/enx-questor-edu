namespace Infrastructure.Telegram;

public sealed class TelegramOptions
{
    public string BotToken { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
}