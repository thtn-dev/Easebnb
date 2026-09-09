using BuildingBlocks.SharedKernel.Common;

namespace Easebnb.Organization.Core.Entities;

/// <summary>
///     A user's role inside one organization. This is organization-level
///     access control only and is deliberately unrelated to the ASP.NET
///     Core Identity roles owned by the Identity module.
/// </summary>
public enum OrganizationMemberRole
{
    Owner,
    Admin,
    Manager,
    Member
}

/// <summary>
///     Membership join between an organization and a user. The role is the
///     single source of truth for what the user may do inside the
///     organization; the Owner role is additionally mirrored on
///     <see cref="Organization.OwnerUserId" />.
/// </summary>
public sealed class OrganizationMember : IEntityBase<Guid>, IAuditableEntity
{
    private OrganizationMember()
    {
    }

    private OrganizationMember(Guid organizationId, Guid userId, OrganizationMemberRole role)
    {
        Id = Guid.CreateVersion7(DateTimeOffset.UtcNow);
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; private set; }

    /// <summary>Logical reference to the Identity module's user (no FK across module schemas).</summary>
    public Guid UserId { get; private set; }

    public OrganizationMemberRole Role { get; private set; }

    /// <summary>When the user joined the organization.</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public static OrganizationMember Create(Guid organizationId, Guid userId, OrganizationMemberRole role)
    {
        return new OrganizationMember(organizationId, userId, role);
    }

    public void ChangeRole(OrganizationMemberRole role)
    {
        Role = role;
    }
}
