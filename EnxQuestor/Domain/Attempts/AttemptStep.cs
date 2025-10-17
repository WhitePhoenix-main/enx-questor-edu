using System;
using Domain.Common;

namespace Domain.Attempts;

public sealed class AttemptStep : Entity<Guid>
{
    public Guid AttemptId { get; private set; }
    public Guid StepId { get; private set; }
    public string AnswerJson { get; private set; } = "{}";
    public bool? IsCorrect { get; private set; }
    public int ScoreAwarded { get; private set; }
    public string? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    public static AttemptStep Create(Guid attemptId, Guid stepId) =>
        new() { Id = Guid.NewGuid(), AttemptId = attemptId, StepId = stepId };

    public void SaveAutoCheck(string answerJson, bool isCorrect, int score)
    {
        AnswerJson = answerJson;
        IsCorrect = isCorrect;
        ScoreAwarded = score;
    }

    public void SetManualReview(string reviewerId, int score)
    {
        ReviewedBy = reviewerId;
        ReviewedAt = DateTimeOffset.UtcNow;
        ScoreAwarded = score;
    }
}