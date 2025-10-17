using System;
using Domain.Common;

namespace Domain.Telegram;

public sealed class TelegramLink : Entity<Guid>
{
    public string UserId { get; private set; } = default!;
    public string OneTimeCode { get; private set; } = default!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }

    public static TelegramLink Create(string userId, string code, DateTimeOffset expiresAt) => new()
        { Id = Guid.NewGuid(), UserId = userId, OneTimeCode = code, ExpiresAt = expiresAt };

    public bool TryConsume()
    {
        if (ConsumedAt != null || DateTimeOffset.UtcNow > ExpiresAt) return false;
        ConsumedAt = DateTimeOffset.UtcNow;
        return true;
    }
}