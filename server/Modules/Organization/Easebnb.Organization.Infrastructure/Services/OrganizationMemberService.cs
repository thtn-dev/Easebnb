using BuildingBlocks.Application;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.Organization.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Easebnb.Organization.Infrastructure.Services;

public sealed class OrganizationMemberService(
    OrganizationDbContext dbContext)
    : IOrganizationMemberService
{
    public async Task<ErrorOr<PaginatedResponse<OrganizationMemberResponse>>> GetMembersAsync(
        Guid organizationId,
        Guid currentUserId,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
            return Error.NotFound(description: "Organization not found");

        var membership = await FindMembershipAsync(organizationId, currentUserId, cancellationToken);
        var accessError = OrganizationAccess.EnsureMember(membership);
        if (accessError is not null) return accessError.Value;

        var query =
            from member in dbContext.OrganizationMembers
            where member.OrganizationId == organizationId
            join registeredUser in dbContext.RegisteredUsers
                on member.UserId equals registeredUser.Id into users
            from user in users.DefaultIfEmpty()
            orderby member.CreatedAt
            select new OrganizationMemberResponse(
                member.UserId,
                user != null ? user.UserName : null,
                user != null ? user.Email : null,
                member.Role.ToString(),
                member.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(pageRequest.Skip)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken);

        return PaginatedResponse<OrganizationMemberResponse>.Ok(
            items,
            PaginationMetadata.Create(pageRequest.Page, pageRequest.PageSize, totalItems));
    }

    public async Task<ErrorOr<OrganizationMemberResponse>> AddMemberAsync(
        Guid organizationId,
        Guid currentUserId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
            return Error.NotFound(description: "Organization not found");

        var membership = await FindMembershipAsync(organizationId, currentUserId, cancellationToken);

        var accessError = OrganizationAccess.EnsureMember(membership)
                          ?? (membership is null ? null : OrganizationAccess.EnsureAdministrator(membership))
                          ?? OrganizationAccess.EnsureActive(organization);
        if (accessError is not null) return accessError.Value;

        if (!TryParseRole(request.Role, out var role))
            return Error.Validation(description: "Invalid organization member role");

        if (role == OrganizationMemberRole.Owner)
            return Error.Conflict(
                description: "The organization already has an owner; transfer ownership instead of adding another one");

        if (!await dbContext.RegisteredUsers.AnyAsync(u => u.Id == request.UserId, cancellationToken))
            return Error.NotFound(description: "User not found");

        var alreadyMember = await dbContext.OrganizationMembers
            .AnyAsync(
                m => m.OrganizationId == organizationId && m.UserId == request.UserId,
                cancellationToken);
        if (alreadyMember)
            return Error.Conflict(description: "User is already a member of this organization");

        dbContext.OrganizationMembers.Add(
            OrganizationMember.Create(organizationId, request.UserId, role));
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMemberResponseAsync(organizationId, request.UserId, cancellationToken);
    }

    public async Task<ErrorOr<OrganizationMemberResponse>> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid currentUserId,
        Guid targetUserId,
        UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
            return Error.NotFound(description: "Organization not found");

        var membership = await FindMembershipAsync(organizationId, currentUserId, cancellationToken);

        var accessError = OrganizationAccess.EnsureMember(membership)
                          ?? (membership is null ? null : OrganizationAccess.EnsureAdministrator(membership))
                          ?? OrganizationAccess.EnsureActive(organization);
        if (accessError is not null) return accessError.Value;

        if (!TryParseRole(request.Role, out var newRole))
            return Error.Validation(description: "Invalid organization member role");

        var target = await dbContext.OrganizationMembers
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == targetUserId,
                cancellationToken);

        if (target is null)
            return Error.NotFound(description: "Member not found");

        if (target.Role == newRole)
            return Error.Validation(description: "Member already has this role");

        if (target.UserId == organization.OwnerUserId && newRole != OrganizationMemberRole.Owner)
            return Error.Conflict(
                description: "The owner's role cannot be changed; transfer ownership to another member instead");

        if (newRole == OrganizationMemberRole.Owner)
        {
            if (membership!.Role != OrganizationMemberRole.Owner)
                return Error.Forbidden(description: "Only the owner can transfer ownership");

            var currentOwner = await dbContext.OrganizationMembers
                .FirstOrDefaultAsync(
                    m => m.OrganizationId == organizationId && m.UserId == organization.OwnerUserId,
                    cancellationToken);
            if (currentOwner is null)
                return Error.Unexpected(description: "Organization owner membership is missing");

            // Ownership transfer: demote the current owner, promote the
            // target and move the denormalized owner reference — all in one
            // SaveChanges, so the invariant never breaks.
            currentOwner.ChangeRole(OrganizationMemberRole.Admin);
            target.ChangeRole(OrganizationMemberRole.Owner);
            organization.ChangeOwner(target.UserId);

            await dbContext.SaveChangesAsync(cancellationToken);
            return await GetMemberResponseAsync(organizationId, targetUserId, cancellationToken);
        }

        // Non-owner changes: admins may only manage managers and members.
        if (membership!.Role == OrganizationMemberRole.Admin)
        {
            if (OrganizationAccess.IsAdministrator(target.Role))
                return Error.Forbidden(
                    description: "Admins can only manage managers and members");

            if (OrganizationAccess.IsAdministrator(newRole))
                return Error.Forbidden(
                    description: "Only the owner can grant administrator roles");
        }

        target.ChangeRole(newRole);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetMemberResponseAsync(organizationId, targetUserId, cancellationToken);
    }

    public async Task<ErrorOr<Success>> RemoveMemberAsync(
        Guid organizationId,
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
            return Error.NotFound(description: "Organization not found");

        var membership = await FindMembershipAsync(organizationId, currentUserId, cancellationToken);

        var accessError = OrganizationAccess.EnsureMember(membership)
                          ?? (membership is null ? null : OrganizationAccess.EnsureAdministrator(membership))
                          ?? OrganizationAccess.EnsureActive(organization);
        if (accessError is not null) return accessError.Value;

        var target = await dbContext.OrganizationMembers
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == targetUserId,
                cancellationToken);

        if (target is null)
            return Error.NotFound(description: "Member not found");

        if (target.UserId == organization.OwnerUserId)
            return Error.Conflict(
                description: "Cannot remove the organization owner; transfer ownership first");

        if (membership!.Role == OrganizationMemberRole.Admin &&
            OrganizationAccess.IsAdministrator(target.Role))
            return Error.Forbidden(description: "Admins can only remove managers and members");

        dbContext.OrganizationMembers.Remove(target);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }

    private Task<OrganizationMember?> FindMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == userId,
                cancellationToken);
    }

    private async Task<ErrorOr<OrganizationMemberResponse>> GetMemberResponseAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var member = await dbContext.OrganizationMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == userId,
                cancellationToken);

        if (member is null)
            return Error.NotFound(description: "Member not found");

        var registeredUser = await dbContext.RegisteredUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return new OrganizationMemberResponse(
            member.UserId,
            registeredUser?.UserName,
            registeredUser?.Email,
            member.Role.ToString(),
            member.CreatedAt);
    }

    private static bool TryParseRole(string value, out OrganizationMemberRole role)
    {
        return Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);
    }
}
