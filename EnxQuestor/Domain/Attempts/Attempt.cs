using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Common;

namespace Domain.Attempts;

public sealed class Attempt : Entity<Guid>
{
    public Guid ScenarioId { get; private set; }
    public string UserId { get; private set; } = default!;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public int Score { get; private set; }
    public AttemptStatus Status { get; private set; }
    private readonly List<AttemptStep> _steps = new();
    public IReadOnlyList<AttemptStep> Steps => _steps;

    public static Attempt Start(Guid scenarioId, string userId, IEnumerable<Guid> stepIds)
    {
        var a = new Attempt
        {
            Id = Guid.NewGuid(), ScenarioId = scenarioId, UserId = userId, StartedAt = DateTimeOffset.UtcNow,
            Status = AttemptStatus.InProgress
        };
        a._steps.AddRange(stepIds.Select(id => AttemptStep.Create(a.Id, id)));
        return a;
    }

    public void AwardScore(int delta) => Score += delta;

    public void Finish()
    {
        if (Status != AttemptStatus.InProgress) return;
        Status = AttemptStatus.Completed;
        FinishedAt = DateTimeOffset.UtcNow;
    }
}