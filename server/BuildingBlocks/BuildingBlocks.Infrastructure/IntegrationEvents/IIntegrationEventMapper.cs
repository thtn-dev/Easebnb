using BuildingBlocks.IntegrationEvents;
using BuildingBlocks.SharedKernel;

namespace BuildingBlocks.Infrastructure.IntegrationEvents;

/// <summary>
///     Maps a domain event to zero or more integration events. Implementations
///     live in the publishing module and are picked up automatically by
///     <see cref="IntegrationEventPublisherBridge{TDomainEvent}" /> — adding a
///     new mapping is a single class, no pipeline code changes.
/// </summary>
/// <typeparam name="TDomainEvent">The domain event to translate.</typeparam>
public interface IIntegrationEventMapper<in TDomainEvent> where TDomainEvent : IDomainEvent
{
    /// <summary>
    ///     Translate the domain event. Return an empty sequence when no
    ///     integration event should be published for it.
    /// </summary>
    IReadOnlyList<IIntegrationEvent> Map(TDomainEvent domainEvent);
}
