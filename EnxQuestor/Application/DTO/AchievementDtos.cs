using System;

namespace Application.DTO;

public record AchievementDto(string Code, string Title, string Description, string? IconUrl, DateTimeOffset AwardedAt);