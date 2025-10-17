using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Common;

namespace Domain.Scenarios;

public sealed class Scenario : Entity<Guid>
{
    public string Title { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string Description { get; private set; } = "";
    public string Tags { get; private set; } = "";
    public int Difficulty { get; private set; }
    public bool IsPublished { get; private set; }
    public string AuthorId { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private readonly List<ScenarioStep> _steps = new();
    public IReadOnlyList<ScenarioStep> Steps => _steps;

    public static Scenario Create(string title, string slug, string authorId, int difficulty, string description = "",
        string tags = "")
    {
        return new Scenario
        {
            Id = Guid.NewGuid(), Title = title, Slug = slug, AuthorId = authorId, Difficulty = difficulty,
            Description = description, Tags = tags, CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow, IsPublished = false
        };
    }

    public void Publish(bool value)
    {
        IsPublished = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSteps(IEnumerable<ScenarioStep> steps)
    {
        _steps.Clear();
        _steps.AddRange(steps.OrderBy(s => s.Order));
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}