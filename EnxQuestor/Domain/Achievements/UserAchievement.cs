using System;
using Domain.Common;

namespace Domain.Achievements;

public sealed class UserAchievement : Entity<Guid>
{
    public string UserId { get; private set; } = default!;
    public Guid AchievementId { get; private set; }
    public DateTimeOffset AwardedAt { get; private set; }

    public static UserAchievement Award(string userId, Guid achievementId) => new()
        { Id = Guid.NewGuid(), UserId = userId, AchievementId = achievementId, AwardedAt = DateTimeOffset.UtcNow };
}