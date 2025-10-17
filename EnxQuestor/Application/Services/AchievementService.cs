using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Domain.Achievements;
using Microsoft.EntityFrameworkCore;

// алиасы, чтобы не писать полные имена
using Attempt = Domain.Attempts.Attempt;
using AttemptStatus = Domain.Attempts.AttemptStatus;
using AttemptStep = Domain.Attempts.AttemptStep;

namespace Application.Services;

public sealed class AchievementService : IAchievementService
{
    private readonly DbContext _db;
    public AchievementService(DbContext db) => _db = db;

    public async Task CheckAndAwardAsync(Guid attemptId, string userId, CancellationToken ct)
    {
        var achievements = await _db.Set<Achievement>().ToListAsync(ct);

        foreach (var ach in achievements)
        {
            // уже выдано — пропускаем
            bool already = await _db.Set<UserAchievement>()
                .AnyAsync(x => x.UserId == userId && x.AchievementId == ach.Id, ct);
            if (already) continue;

            var rule = JsonDocument.Parse(ach.RuleJson).RootElement;
            var type = rule.GetProperty("type").GetString();

            bool satisfied = type switch
            {
                "FirstCompletion"     => await IsFirstCompletion(userId, ct),
                "CompleteScenarios"   => await CompletedCountAtLeast(
                                            userId, rule.GetProperty("count").GetInt32(), ct),
                "PerfectTest"         => await AttemptIsPerfect(attemptId, ct),
                _                     => false
            };

            if (satisfied)
                _db.Add(UserAchievement.Award(userId, ach.Id));
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<bool> IsFirstCompletion(string userId, CancellationToken ct)
    {
        var q = _db.Set<Attempt>()
            .Where(a => a.UserId == userId && a.Status == AttemptStatus.Completed);

        // Если FinishedAt nullable, даём явный ключ для OrderBy
        return !await q
            .OrderBy(a => a.FinishedAt ?? DateTimeOffset.MaxValue)
            .Skip(1)
            .AnyAsync(ct);
    }

    private async Task<bool> CompletedCountAtLeast(string userId, int count, CancellationToken ct)
    {
        var q = _db.Set<Attempt>()
            .Where(a => a.UserId == userId && a.Status == AttemptStatus.Completed);

        var completed = await q.CountAsync(ct); // без предиката — однозначная перегрузка EF
        return completed >= count;
    }

    private async Task<bool> AttemptIsPerfect(Guid attemptId, CancellationToken ct)
    {
        var steps = await _db.Set<AttemptStep>()
            .Where(s => s.AttemptId == attemptId)
            .ToListAsync(ct);

        return steps.Count > 0 && steps.All(s => s.IsCorrect == true);
    }
}
