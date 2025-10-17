using System;
using Domain.Common;

namespace Domain.Achievements;

public sealed class Achievement : Entity<Guid>
{
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Description { get; private set; } = "";
    public string? IconUrl { get; private set; }
    public string RuleJson { get; private set; } = "{}";

    public static Achievement Create(string code, string title, string ruleJson, string? iconUrl = null,
        string? desc = null)
        => new()
        {
            Id = Guid.NewGuid(), Code = code, Title = title, RuleJson = ruleJson, IconUrl = iconUrl,
            Description = desc ?? ""
        };
}