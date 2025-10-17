using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Common;
using Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Events;

public sealed class DomainEventPublisher : IDomainEventPublisher
{
    private readonly IServiceProvider _sp;
    public DomainEventPublisher(IServiceProvider sp) => _sp = sp;

    public async Task PublishAsync(DomainEvent evt, CancellationToken ct = default)
    {
        if (evt is Domain.Attempts.Events.AttemptCompletedEvent e)
        {
            using var scope = _sp.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<Application.Abstractions.IAchievementService>();
            await svc.CheckAndAwardAsync(e.AttemptId, e.UserId, ct);
        }
    }
}