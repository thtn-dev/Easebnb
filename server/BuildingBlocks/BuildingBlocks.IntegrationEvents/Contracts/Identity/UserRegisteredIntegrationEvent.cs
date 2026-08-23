namespace BuildingBlocks.IntegrationEvents.Contracts.Identity;

/// <summary>
///     Published by the Identity module after a new user account has been
///     committed. Consumed by other modules to maintain their own user
///     projections (e.g. the Organization module's registered-user registry).
///     <para>
///         Versioning: add new fields as nullable properties WITHOUT
///         <c>required</c> so older publishers/consumers stay compatible.
///     </para>
/// </summary>
public sealed record UserRegisteredIntegrationEvent : IntegrationEventBase
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    // Optional since v1 — demonstrates the nullable-field versioning rule.
    public string? UserName { get; init; }
}
