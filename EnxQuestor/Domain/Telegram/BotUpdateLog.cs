using System;
using Domain.Common;

namespace Domain.Telegram;

public sealed class BotUpdateLog : Entity<Guid>
{
    public long UpdateId { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public string Type { get; private set; } = "";
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static BotUpdateLog Create(long id, string type, string payloadJson) => new()
    {
        Id = Guid.NewGuid(), UpdateId = id, Type = type, PayloadJson = payloadJson, ReceivedAt = DateTimeOffset.UtcNow
    };

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;
}