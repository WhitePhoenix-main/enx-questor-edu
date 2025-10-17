
using System;
using System.Threading;
using System.Threading.Tasks;
using Application.DTO;
namespace Application.Abstractions;
public interface IAttemptService
{
    Task<StartAttemptResponse> StartAsync(string userId, StartAttemptRequest req, CancellationToken ct);
    Task<AnswerResponse> AnswerAsync(string userId, AnswerRequest req, CancellationToken ct);
    Task<FinishAttemptResponse> FinishAsync(string userId, FinishAttemptRequest req, CancellationToken ct);
    Task<AttemptViewDto> GetAsync(string userId, Guid attemptId, CancellationToken ct);
}
