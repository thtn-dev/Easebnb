using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.DomainEvent;

public sealed class DomainEventDispatcher(
    IDomainEventsAccessor domainEventsAccessor,
    IPublisher publisher,
    ILogger<DomainEventDispatcher> logger)
    : IDomainEventDispatcher
{
    public async Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default)
    {
        do
        {
            List<IDomainEvent> domainEvents = [.. domainEventsAccessor.GetAllDomainEvents()];
            if (domainEvents.Count == 0) break;

            domainEventsAccessor.ClearAllDomainEvents();

            foreach (var domainEvent in domainEvents)
            {
                logger.LogInformation("Dispatching domain event {EventType}", domainEvent.GetType().Name);
                await publisher.Publish(domainEvent, cancellationToken);
            }
        } while (true);
    }
}