using System;
using System.Collections.Generic;

namespace Application.DTO;

public record ScenarioListItemDto(Guid Id, string Title, string Slug, string Tags, int Difficulty, bool IsPublished);

public record ScenarioDetailsDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    string Tags,
    int Difficulty,
    bool IsPublished,
    IReadOnlyList<ScenarioStepDto> Steps);

public record ScenarioStepDto(Guid Id, int Order, string? Title, string StepType, string Content, int MaxScore);