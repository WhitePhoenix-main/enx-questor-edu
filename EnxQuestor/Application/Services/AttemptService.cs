using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Abstractions;
using Application.DTO;
using Domain.Attempts;
using Domain.Attempts.Events;
using Domain.Common;
using Domain.Scenarios;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class AttemptService : IAttemptService
{
    private readonly DbContext _db;
    private readonly IDomainEventPublisher _events;

    public AttemptService(DbContext db, IDomainEventPublisher events)
    {
        _db = db;
        _events = events;
    }

    public async Task<StartAttemptResponse> StartAsync(string userId, StartAttemptRequest req, CancellationToken ct)
    {
        if (req.ScenarioId == Guid.Empty) throw new ArgumentException("ScenarioId is required");
        var scenario = await _db.Set<Scenario>().Include(s => s.Steps)
                           .FirstOrDefaultAsync(s => s.Id == req.ScenarioId && s.IsPublished, ct)
                       ?? throw new InvalidOperationException("Сценарий не найден или не опубликован.");
        var attempt = Attempt.Start(scenario.Id, userId, scenario.Steps.Select(s => s.Id));
        _db.Add(attempt);
        await _db.SaveChangesAsync(ct);
        return new StartAttemptResponse(attempt.Id);
    }

    public async Task<AnswerResponse> AnswerAsync(string userId, AnswerRequest req, CancellationToken ct)
    {
        if (req.AttemptId == Guid.Empty || req.StepId == Guid.Empty) throw new ArgumentException("Invalid ids");
        if (string.IsNullOrWhiteSpace(req.AnswerJson) || req.AnswerJson.Length > 4000)
            throw new ArgumentException("Invalid answer payload");
        var attempt = await _db.Set<Attempt>().Include(a => a.Steps)
                          .FirstOrDefaultAsync(a => a.Id == req.AttemptId && a.UserId == userId, ct)
                      ?? throw new InvalidOperationException("Попытка не найдена.");
        if (attempt.Status != AttemptStatus.InProgress) throw new InvalidOperationException("Попытка уже завершена.");
        var step = attempt.Steps.FirstOrDefault(s => s.StepId == req.StepId) ??
                   throw new InvalidOperationException("Шаг не принадлежит попытке.");
        var scenarioStep = await _db.Set<ScenarioStep>().FirstAsync(x => x.Id == step.StepId, ct);
        (bool isAuto, bool isCorrect, int score) = AutoCheck(scenarioStep, req.AnswerJson);
        if (isAuto)
        {
            step.SaveAutoCheck(req.AnswerJson, isCorrect, score);
            if (score > 0) attempt.AwardScore(score);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            step.SaveAutoCheck(req.AnswerJson, false, 0);
            await _db.SaveChangesAsync(ct);
        }

        return new AnswerResponse(step.IsCorrect ?? false, step.ScoreAwarded);
    }

    public async Task<FinishAttemptResponse> FinishAsync(string userId, FinishAttemptRequest req, CancellationToken ct)
    {
        var attempt = await _db.Set<Attempt>().FirstOrDefaultAsync(a => a.Id == req.AttemptId && a.UserId == userId, ct)
                      ?? throw new InvalidOperationException("Попытка не найдена.");
        attempt.Finish();
        await _db.SaveChangesAsync(ct);
        await _events.PublishAsync(
            new AttemptCompletedEvent(attempt.Id, attempt.UserId, attempt.ScenarioId, attempt.Score), ct);
        return new FinishAttemptResponse(attempt.Id, attempt.Score);
    }

    public async Task<AttemptViewDto> GetAsync(string userId, Guid attemptId, CancellationToken ct)
    {
        var attempt = await _db.Set<Attempt>().Include(a => a.Steps)
                          .FirstOrDefaultAsync(a => a.Id == attemptId && a.UserId == userId, ct)
                      ?? throw new InvalidOperationException("Попытка не найдена.");
        return new AttemptViewDto(attempt.Id, attempt.Status.ToString(), attempt.Score, attempt.StartedAt,
            attempt.FinishedAt,
            attempt.Steps.Select(s => new AttemptStepViewDto(s.StepId, s.AnswerJson, s.IsCorrect, s.ScoreAwarded))
                .ToList());
    }

    private static (bool isAuto, bool isCorrect, int score) AutoCheck(ScenarioStep step, string answerJson)
    {
        var max = step.MaxScore;
        switch (step.StepType)
        {
            case ScenarioStepType.Single:
                var correct = System.Text.Json.JsonDocument.Parse(step.Content).RootElement.GetProperty("correct")
                    .GetString();
                var given = System.Text.Json.JsonDocument.Parse(answerJson).RootElement.GetProperty("choice")
                    .GetString();
                var ok = string.Equals(correct, given, StringComparison.OrdinalIgnoreCase);
                return (true, ok, ok ? max : 0);
            case ScenarioStepType.Multi:
                var cJson = System.Text.Json.JsonDocument.Parse(step.Content).RootElement.GetProperty("correct")
                    .EnumerateArray().Select(e => e.GetString()).Where(s => s != null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var aJson = System.Text.Json.JsonDocument.Parse(answerJson).RootElement.GetProperty("choices")
                    .EnumerateArray().Select(e => e.GetString()).Where(s => s != null)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var ok2 = cJson.SetEquals(aJson);
                return (true, ok2, ok2 ? max : 0);
            case ScenarioStepType.ShortAnswer:
                var keys = System.Text.Json.JsonDocument.Parse(step.Content).RootElement.GetProperty("keywords")
                    .EnumerateArray().Select(e => e.GetString()!.ToLowerInvariant()).ToArray();
                var text = System.Text.Json.JsonDocument.Parse(answerJson).RootElement.GetProperty("text").GetString()!
                    .ToLowerInvariant();
                var ok3 = keys.All(k => text.Contains(k));
                return (true, ok3, ok3 ? max : max / 3);
            default:
                return (false, false, 0);
        }
    }
}