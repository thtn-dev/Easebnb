using BuildingBlocks.IntegrationEvents;
using BuildingBlocks.SharedKernel;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure.IntegrationEvents;

/// <summary>
///     MediatR bridge between domain events and integration events. It runs as
///     an extra <c>INotificationHandler</c> for every domain event dispatched
///     by the UnitOfWork: after the module's own handlers have run, every
///     registered <see cref="IIntegrationEventMapper{TDomainEvent}" /> is
///     consulted and each mapped event is published via MassTransit.
///     With the EF bus outbox configured, the publish is stored in the scoped
///     DbContext and only delivered to the broker after the surrounding
///     transaction commits — so a rollback drops the event instead of leaking it.
/// </summary>
public sealed class IntegrationEventPublisherBridge<TDomainEvent>(
    IPublishEndpoint publishEndpoint,
    IEnumerable<IIntegrationEventMapper<TDomainEvent>> mappers,
    ILogger<IntegrationEventPublisherBridge<TDomainEvent>> logger)
    : INotificationHandler<TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public async Task Handle(TDomainEvent notification, CancellationToken cancellationToken)
    {
        List<IIntegrationEvent> integrationEvents = [];
        foreach (var mapper in mappers)
            integrationEvents.AddRange(mapper.Map(notification));

        if (integrationEvents.Count == 0) return;

        foreach (var integrationEvent in integrationEvents)
        {
            logger.LogInformation(
                "Publishing integration event {IntegrationEventType} {IntegrationEventId} mapped from domain event {DomainEventType} {DomainEventId}",
                integrationEvent.GetType().Name, integrationEvent.Id,
                typeof(TDomainEvent).Name, notification.Id);

            // Publish by runtime type: the generic Publish<T> would target the
            // base IIntegrationEvent exchange, which no consumer binds to.
            await publishEndpoint.Publish(integrationEvent, integrationEvent.GetType(), cancellationToken);
        }
    }
}
