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
    public int Difficulty { get; private set; }                    // <-- int вместо string
    public bool IsPublished { get; private set; }
    public string AuthorId { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ScenarioStep> _steps = new();
    public IReadOnlyList<ScenarioStep> Steps => _steps;            // EF-friendly

    private Scenario() { } // для EF

    public static Scenario Create(
        string title,
        string slug,
        string authorId,
        int difficulty,
        string description = "",
        string tags = "")
    {
        var s = new Scenario
        {
            Id = Guid.NewGuid(),
            AuthorId = GuardAuthor(authorId),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            IsPublished = false
        };
        s.SetTitle(title);
        s.SetSlug(slug);
        s.SetDifficulty(difficulty);
        s.SetDescription(description);
        s.SetTags(tags);
        return s;
    }

    public void Update(
        string title,
        string slug,
        int difficulty,
        string description,
        string tags,
        bool isPublished)
    {
        SetTitle(title);
        SetSlug(slug);
        SetDifficulty(difficulty);
        SetDescription(description);
        SetTags(tags);
        IsPublished = isPublished;
        Touch();
    }

    public void Publish(bool value)
    {
        IsPublished = value;
        Touch();
    }

    public void SetSteps(IEnumerable<ScenarioStep> steps)
    {
        _steps.Clear();
        _steps.AddRange(steps.OrderBy(s => s.Order));
        Touch();
    }

    // ----- инварианты / нормализация -----

    void SetTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        Title = title.Trim();
    }

    void SetSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Slug is required", nameof(slug));
        Slug = slug.Trim().ToLowerInvariant();
    }

    void SetDescription(string description) => Description = (description ?? string.Empty).Trim();

    void SetTags(string tags)
    {
        if (string.IsNullOrWhiteSpace(tags)) { Tags = ""; return; }
        Tags = string.Join(",",
            tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => t.ToLowerInvariant())
                .Distinct());
    }

    void SetDifficulty(int difficulty)
    {
        // при желании поменяй допустимый диапазон
        if (difficulty < 1 || difficulty > 5)
            throw new ArgumentOutOfRangeException(nameof(difficulty), "Difficulty must be in range 1..5.");
        Difficulty = difficulty;
    }

    static string GuardAuthor(string authorId)
    {
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException("AuthorId is required", nameof(authorId));
        return authorId;
    }

    void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
