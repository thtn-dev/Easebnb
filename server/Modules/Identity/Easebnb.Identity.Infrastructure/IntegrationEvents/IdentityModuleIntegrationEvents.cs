using BuildingBlocks.Infrastructure.IntegrationEvents;
using Easebnb.Identity.Core.Events;
using Easebnb.Identity.Infrastructure.Database;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Easebnb.Identity.Infrastructure.IntegrationEvents;

/// <summary>
///     Identity's MassTransit registration, called from the composition root
///     inside <c>AddMassTransit</c>. Registers the module's integration event
///     mappers and the EF transactional outbox on the module's own DbContext.
/// </summary>
public static class IdentityModuleIntegrationEvents
{
    extension(IBusRegistrationConfigurator bus)
    {
        public IBusRegistrationConfigurator AddIdentityModuleIntegrationEvents(IConfiguration configuration)
        {
            bus.TryAddTransient<IIntegrationEventMapper<UserRegisteredDomainEvent>, UserRegisteredIntegrationEventMapper>();

            bus.AddEntityFrameworkOutbox<AppIdentityDbContext>(outbox =>
            {
                // Delivery-service poll interval: lower = events leave sooner,
                // higher = less database load. 2s is a comfortable dev default.
                outbox.QueryDelay = TimeSpan.FromSeconds(2);

                // Identity owns request-scope publishing: IPublishEndpoint calls
                // outside consumers (e.g. from domain-event handlers during an
                // HTTP request) are captured into this module's DbContext and
                // flushed after the transaction commits. Note: MassTransit only
                // supports ONE bus-outbox-bound DbContext per bus — see
                // docs/integration-events.md before adding another.
                outbox.UseBusOutbox();

                // Schema caching must be off when multiple outbox DbContexts
                // (one per module schema) share the same bus.
                outbox.UsePostgres(enableSchemaCaching: false);
            });

            return bus;
        }
    }
}
