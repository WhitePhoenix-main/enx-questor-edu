using System;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity;

public sealed class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? TelegramId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}