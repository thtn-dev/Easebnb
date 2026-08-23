using Easebnb.Organization.Infrastructure.Database;
using Easebnb.Organization.Infrastructure.IntegrationEvents;
using MassTransit;
using Microsoft.Extensions.Configuration;

namespace Easebnb.Organization.Infrastructure;

/// <summary>
///     Organization's MassTransit registration, called from the composition
///     root inside <c>AddMassTransit</c>. Registers the module's consumers and
///     the EF outbox/inbox on the module's own DbContext. Adding a consumer to
///     this module is a one-line change here.
/// </summary>
public static class OrganizationModuleIntegrationEvents
{
    extension(IBusRegistrationConfigurator bus)
    {
        public IBusRegistrationConfigurator AddOrganizationModuleIntegrationEvents(IConfiguration configuration)
        {
            bus.AddConsumer<UserRegisteredConsumer, UserRegisteredConsumerDefinition>();

            bus.AddEntityFrameworkOutbox<OrganizationDbContext>(outbox =>
            {
                outbox.QueryDelay = TimeSpan.FromSeconds(2);

                // Consumer-side inbox: consumed MessageIds are persisted in this
                // module's schema so redelivered messages are filtered before
                // the consumer runs.
                outbox.DuplicateDetectionWindow = TimeSpan.FromHours(1);

                // No UseBusOutbox here: Organization does not publish from
                // request scopes. Only one DbContext per bus may own the bus
                // outbox (Identity currently does).

                // Schema caching must be off when multiple outbox DbContexts
                // (one per module schema) share the same bus.
                outbox.UsePostgres(enableSchemaCaching: false);
            });

            return bus;
        }
    }
}
