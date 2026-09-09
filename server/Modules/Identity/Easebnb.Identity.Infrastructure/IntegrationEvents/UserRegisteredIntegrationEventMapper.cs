using BuildingBlocks.Infrastructure.IntegrationEvents;
using BuildingBlocks.IntegrationEvents;
using BuildingBlocks.IntegrationEvents.Contracts.Identity;
using Easebnb.Identity.Core.Events;

namespace Easebnb.Identity.Infrastructure.IntegrationEvents;

/// <summary>
///     Maps <see cref="UserRegisteredDomainEvent" /> to the shared contract
///     <see cref="UserRegisteredIntegrationEvent" />. Adding a new
///     domain-event mapping is exactly this: one class implementing
///     <see cref="IIntegrationEventMapper{TDomainEvent}" /> — nothing else
///     in the pipeline needs to change.
/// </summary>
public sealed class UserRegisteredIntegrationEventMapper
    : IIntegrationEventMapper<UserRegisteredDomainEvent>
{
    public IReadOnlyList<IIntegrationEvent> Map(UserRegisteredDomainEvent domainEvent) =>
    [
        new UserRegisteredIntegrationEvent
        {
            UserId = domainEvent.UserId,
            Email = domainEvent.Email,
            UserName = domainEvent.UserName
        }
    ];
}
