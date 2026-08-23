using BuildingBlocks.Application;
using Easebnb.Organization.Core.Dtos;

namespace Easebnb.Organization.Core.Interfaces;

public interface IOrganizationMemberService
{
    Task<ErrorOr<PaginatedResponse<OrganizationMemberResponse>>> GetMembersAsync(
        Guid organizationId,
        Guid currentUserId,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<OrganizationMemberResponse>> AddMemberAsync(
        Guid organizationId,
        Guid currentUserId,
        AddOrganizationMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<OrganizationMemberResponse>> ChangeMemberRoleAsync(
        Guid organizationId,
        Guid currentUserId,
        Guid targetUserId,
        UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<Success>> RemoveMemberAsync(
        Guid organizationId,
        Guid currentUserId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);
}
