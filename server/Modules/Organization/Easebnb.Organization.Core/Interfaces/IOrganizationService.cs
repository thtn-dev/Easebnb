using BuildingBlocks.Application;
using Easebnb.Organization.Core.Dtos;

namespace Easebnb.Organization.Core.Interfaces;

public interface IOrganizationService
{
    Task<ErrorOr<OrganizationResponse>> CreateOrganizationAsync(
        Guid currentUserId,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<OrganizationResponse>> GetOrganizationByIdAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<OrganizationResponse>> GetOrganizationBySlugAsync(
        string slug,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<PaginatedResponse<OrganizationSummaryResponse>>> GetMyOrganizationsAsync(
        Guid currentUserId,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<OrganizationResponse>> UpdateOrganizationAsync(
        Guid organizationId,
        Guid currentUserId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<ErrorOr<Success>> ArchiveOrganizationAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Uploads the organization logo to object storage and stores the
    ///     resulting object key. The stream must already be validated and
    ///     re-encoded by the caller (same contract as the Identity module's
    ///     profile picture upload).
    /// </summary>
    Task<ErrorOr<OrganizationResponse>> UpdateLogoAsync(
        Guid organizationId,
        Guid currentUserId,
        Stream content,
        CancellationToken cancellationToken = default);
}
