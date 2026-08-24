using BuildingBlocks.SharedKernel.Common;

namespace Easebnb.Organization.Core.Entities;

/// <summary>
///     Local projection of a registered user, fed by the
///     UserRegisteredIntegrationEvent published by the Identity module.
///     The primary key is the Identity user id. It lets this module answer
///     "does this user exist?" and enrich member listings without ever
///     referencing the Identity schema. Eventually consistent by design.
/// </summary>
public sealed class RegisteredUser : IEntityBase<Guid>, IAuditableEntity
{
    private RegisteredUser()
    {
    }

    public Guid Id { get; set; }

    public string Email { get; private set; } = null!;

    public string? UserName { get; private set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public static RegisteredUser Create(Guid userId, string email, string? userName)
    {
        return new RegisteredUser
        {
            Id = userId,
            Email = email,
            UserName = userName
        };
    }

    public void Update(string email, string? userName)
    {
        Email = email;
        UserName = userName;
    }
}
