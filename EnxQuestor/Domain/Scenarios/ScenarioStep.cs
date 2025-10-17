using System;
using Domain.Common;

namespace Domain.Scenarios;

public sealed class ScenarioStep : Entity<Guid>
{
    public Guid ScenarioId { get; private set; }
    public int Order { get; private set; }
    public ScenarioStepType StepType { get; private set; }
    public string? Title { get; private set; }
    public string Content { get; private set; } = "{}";
    public int MaxScore { get; private set; }

    public static ScenarioStep Create(Guid scenarioId, int order, ScenarioStepType type, string content, int maxScore,
        string? title = null)
        => new()
        {
            Id = Guid.NewGuid(), ScenarioId = scenarioId, Order = order, StepType = type, Content = content,
            MaxScore = maxScore, Title = title
        };
}