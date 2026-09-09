using BuildingBlocks.SharedKernel;

namespace Easebnb.Identity.Core.Events;

/// <summary>
///     Domain event raised when a new user account has been created, before
///     the transaction commits. Internal handlers react in-process; the
///     integration-event bridge additionally publishes
///     <c>UserRegisteredIntegrationEvent</c> so other modules learn about the
///     new user.
/// </summary>
public sealed class UserRegisteredDomainEvent(Guid userId, string email, string? userName)
    : DomainEventBase
{
    public Guid UserId { get; } = userId;
    public string Email { get; } = email;
    public string? UserName { get; } = userName;
}
