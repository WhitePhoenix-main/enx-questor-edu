// Web/Identity/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;
using System;

namespace Web.Identity;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? TelegramId { get; set; }
    public string? TelegramUsername { get; set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}