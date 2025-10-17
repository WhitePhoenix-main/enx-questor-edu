using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Abstractions;

public interface IAchievementService
{
    Task CheckAndAwardAsync(Guid attemptId, string userId, CancellationToken ct);
}