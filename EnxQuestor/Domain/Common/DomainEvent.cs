using System;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.Common;

public abstract record DomainEvent(DateTimeOffset OccurredAt);

public interface IDomainEventPublisher
{
    Task PublishAsync(DomainEvent evt, CancellationToken ct = default);
}