using BuildingBlocks.SharedKernel;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BuildingBlocks.Infrastructure.IntegrationEvents;

/// <summary>
///     Composition-root wiring for the integration event bus. Transport is
///     RabbitMQ when <c>RabbitMq:Enabled</c> is true, otherwise the in-memory
///     transport (development/tests without a broker). Module registrations
///     (consumers, outbox contexts, mappers) hang off the same
///     <c>AddMassTransit</c> call via their own
///     <c>Add&lt;Module&gt;ModuleIntegrationEvents</c> extensions.
/// </summary>
public static class IntegrationEventBusExtensions
{
    extension(IBusRegistrationConfigurator bus)
    {
        public IBusRegistrationConfigurator AddIntegrationEventBus(IConfiguration configuration)
        {
            bus.AddOptions<RabbitMqSettings>()
                .Bind(configuration.GetSection(RabbitMqSettings.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // Domain Event -> Integration Event bridge: runs for every
            // IDomainEvent publication; without mappers it is a no-op.
            bus.TryAddTransient(typeof(INotificationHandler<>), typeof(IntegrationEventPublisherBridge<>));

            bus.SetKebabCaseEndpointNameFormatter();

            var settings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>()
                           ?? new RabbitMqSettings();

            if (settings.Enabled)
            {
                bus.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(settings.Host, (ushort)settings.Port, settings.VirtualHost, host =>
                    {
                        host.Username(settings.Username);
                        host.Password(settings.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                // Same pipeline, no broker: publishes are delivered in-process.
                bus.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
            }

            return bus;
        }
    }
}
