namespace BuildingBlocks.IntegrationEvents;

/// <summary>
///     Marker contract for an integration event exchanged between modules.
///     Integration events are facts about something that happened inside a
///     module; other modules subscribe to them via the message broker.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    ///     Unique identifier of this event instance.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    ///     UTC timestamp of when the event occurred.
    /// </summary>
    DateTime OccurredOn { get; }
}

/// <summary>
///     Base record for integration event contracts.
///     Naming: past tense with the <c>IntegrationEvent</c> suffix
///     (e.g. <see cref="Contracts.Identity.UserRegisteredIntegrationEvent"/>).
///     Versioning: fields added after release must be nullable/optional so
///     consumers compiled against the previous contract keep working.
/// </summary>
public abstract record IntegrationEventBase : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}
