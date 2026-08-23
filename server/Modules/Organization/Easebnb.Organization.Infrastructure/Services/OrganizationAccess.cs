namespace Easebnb.Organization.Infrastructure.Services;

using Easebnb.Organization.Core.Entities;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

/// <summary>
///     Shared membership/status checks for the module's services. Each check
///     returns the violated rule as an <see cref="Error" />, or null when it
///     passes, so call sites stay one-liners and both services enforce the
///     exact same rules.
/// </summary>
internal static class OrganizationAccess
{
    public static Error? EnsureActive(Organization organization)
    {
        return organization.IsActive
            ? null
            : Error.Conflict(description: "Organization is not active");
    }

    public static Error? EnsureMember(OrganizationMember? membership)
    {
        return membership is null
            ? Error.Forbidden(description: "You are not a member of this organization")
            : null;
    }

    /// <summary>Owner or Admin: may manage organization details and members.</summary>
    public static Error? EnsureAdministrator(OrganizationMember membership)
    {
        return IsAdministrator(membership.Role)
            ? null
            : Error.Forbidden(description: "You do not have permission to manage this organization");
    }

    public static Error? EnsureOwner(OrganizationMember membership)
    {
        return membership.Role == OrganizationMemberRole.Owner
            ? null
            : Error.Forbidden(description: "Only the organization owner can perform this action");
    }

    public static bool IsAdministrator(OrganizationMemberRole role)
    {
        return role is OrganizationMemberRole.Owner or OrganizationMemberRole.Admin;
    }
}
