using System;
using Domain.Common;

namespace Domain.Attempts.Events;

public sealed record AttemptCompletedEvent(Guid AttemptId, string UserId, Guid ScenarioId, int Score)
    : DomainEvent(DateTimeOffset.UtcNow);