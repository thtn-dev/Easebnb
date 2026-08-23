using BuildingBlocks.IntegrationEvents.Contracts.Identity;
using Easebnb.Organization.Infrastructure.Database;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Easebnb.Organization.Infrastructure.IntegrationEvents;

/// <summary>
///     Consumes <see cref="UserRegisteredIntegrationEvent" /> published by the
///     Identity module. Deliberately minimal (log-only) so it serves as the
///     reference consumer: real consumers replace the logging with their own
///     application logic. Delivery is at-least-once, so any real handling must
///     stay idempotent — the EF inbox configured in
///     <see cref="UserRegisteredConsumerDefinition" /> filters redeliveries by
///     MessageId.
/// </summary>
public sealed class UserRegisteredConsumer(ILogger<UserRegisteredConsumer> logger)
    : IConsumer<UserRegisteredIntegrationEvent>
{
    public Task Consume(ConsumeContext<UserRegisteredIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Consuming UserRegisteredIntegrationEvent {MessageId} for user {UserId} (correlation {CorrelationId}, event {EventId})",
            context.MessageId, message.UserId, context.CorrelationId, message.Id);

        return Task.CompletedTask;
    }
}

/// <summary>
///     Endpoint configuration for <see cref="UserRegisteredConsumer" />:
///     exponential retry on the receive endpoint plus the EF transactional
///     outbox/inbox bound to this module's DbContext. New consumers follow the
///     same two-class pattern (consumer + definition).
/// </summary>
public sealed class UserRegisteredConsumerDefinition : ConsumerDefinition<UserRegisteredConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<UserRegisteredConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(retry => retry.Exponential(
            retryLimit: 5,
            minInterval: TimeSpan.FromSeconds(1),
            maxInterval: TimeSpan.FromSeconds(30),
            intervalDelta: TimeSpan.FromSeconds(2)));

        endpointConfigurator.UseEntityFrameworkOutbox<OrganizationDbContext>(context);
    }
}
