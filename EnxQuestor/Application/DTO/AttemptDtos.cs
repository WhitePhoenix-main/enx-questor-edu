using System;
using System.Collections.Generic;

namespace Application.DTO;

public record StartAttemptRequest(Guid ScenarioId);

public record StartAttemptResponse(Guid AttemptId);

public record AnswerRequest(Guid AttemptId, Guid StepId, string AnswerJson);

public record AnswerResponse(bool IsCorrect, int ScoreAwarded);

public record FinishAttemptRequest(Guid AttemptId);

public record FinishAttemptResponse(Guid AttemptId, int TotalScore);

public record AttemptViewDto(
    Guid Id,
    string Status,
    int Score,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<AttemptStepViewDto> Steps);

public record AttemptStepViewDto(Guid StepId, string AnswerJson, bool? IsCorrect, int ScoreAwarded);