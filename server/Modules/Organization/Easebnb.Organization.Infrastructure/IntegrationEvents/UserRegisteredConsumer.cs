using BuildingBlocks.IntegrationEvents.Contracts.Identity;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Infrastructure.Database;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Easebnb.Organization.Infrastructure.IntegrationEvents;

/// <summary>
///     Consumes <see cref="UserRegisteredIntegrationEvent" /> published by the
///     Identity module and upserts the user into this module's registered-user
///     registry (<c>organization.registered_users</c>). The registry backs the
///     "user must exist" rule when adding organization members and enriches
///     member listings, without this module ever touching the Identity schema.
///     Delivery is at-least-once, so handling must stay idempotent — the EF
///     inbox configured in <see cref="UserRegisteredConsumerDefinition" />
///     filters redeliveries by MessageId, and the primary-key upsert tolerates
///     any that still slip through.
/// </summary>
public sealed class UserRegisteredConsumer(
    ILogger<UserRegisteredConsumer> logger,
    OrganizationDbContext dbContext)
    : IConsumer<UserRegisteredIntegrationEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Consuming UserRegisteredIntegrationEvent {MessageId} for user {UserId} (correlation {CorrelationId}, event {EventId})",
            context.MessageId, message.UserId, context.CorrelationId, message.Id);

        var registeredUser = await dbContext.RegisteredUsers
            .FirstOrDefaultAsync(u => u.Id == message.UserId, context.CancellationToken);

        if (registeredUser is null)
            dbContext.RegisteredUsers.Add(
                RegisteredUser.Create(message.UserId, message.Email, message.UserName));
        else
            registeredUser.Update(message.Email, message.UserName);

        // Saved together with the inbox state by the EF outbox middleware so
        // the projection and the consumed marker commit atomically.
        await dbContext.SaveChangesAsync(context.CancellationToken);
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
