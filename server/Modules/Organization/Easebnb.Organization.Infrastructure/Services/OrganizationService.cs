using BuildingBlocks.Application;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using BuildingBlocks.Infrastructure;
using BuildingBlocks.Infrastructure.ObjectStorage.S3;
using BuildingBlocks.SharedKernel;
using Easebnb.Organization.Core.Common;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.Organization.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Easebnb.Organization.Infrastructure.Services;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

public sealed class OrganizationService(
    OrganizationDbContext dbContext,
    [FromKeyedServices("Organization")] IUnitOfWork unitOfWork,
    IObjectStorage objectStorage,
    ILogger<OrganizationService> logger) : IOrganizationService
{
    private const string LogoBucket = "easebnb-organizations";

    public async Task<ErrorOr<OrganizationResponse>> CreateOrganizationAsync(
        Guid currentUserId,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? OrganizationSlug.FromName(request.Name)
            : OrganizationSlug.Normalize(request.Slug);

        var slugTaken = await dbContext.Organizations
            .AnyAsync(o => o.Slug == slug, cancellationToken);
        if (slugTaken)
            return Error.Conflict(description: $"Organization slug '{slug}' already exists");

        var organization = Organization.Create(request.Name, slug, request.Description, currentUserId);
        var ownerMembership = OrganizationMember.Create(
            organization.Id, currentUserId, OrganizationMemberRole.Owner);

        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            dbContext.Organizations.Add(organization);
            dbContext.OrganizationMembers.Add(ownerMembership);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            // RollbackTransactionAsync is a no-op once the commit released
            // the transaction.
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return ToResponse(organization);
    }

    public async Task<ErrorOr<OrganizationResponse>> GetOrganizationByIdAsync(
        Guid organizationId,
        Guid currentUserId,
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

        return ToResponse(organization);
    }

    public async Task<ErrorOr<OrganizationResponse>> GetOrganizationBySlugAsync(
        string slug,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Slug == OrganizationSlug.Normalize(slug), cancellationToken);

        if (organization is null)
            return Error.NotFound(description: "Organization not found");

        var membership = await FindMembershipAsync(organization.Id, currentUserId, cancellationToken);
        var accessError = OrganizationAccess.EnsureMember(membership);
        if (accessError is not null) return accessError.Value;

        return ToResponse(organization);
    }

    public async Task<ErrorOr<PaginatedResponse<OrganizationSummaryResponse>>> GetMyOrganizationsAsync(
        Guid currentUserId,
        PagedRequest pageRequest,
        CancellationToken cancellationToken = default)
    {
        var query =
            from membership in dbContext.OrganizationMembers
            where membership.UserId == currentUserId
            join organization in dbContext.Organizations
                on membership.OrganizationId equals organization.Id
            orderby organization.CreatedAt descending
            select new OrganizationSummaryResponse(
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.LogoKey,
                organization.Status.ToString(),
                membership.Role.ToString(),
                organization.CreatedAt);

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(pageRequest.Skip)
            .Take(pageRequest.PageSize)
            .ToListAsync(cancellationToken);

        return PaginatedResponse<OrganizationSummaryResponse>.Ok(
            items,
            PaginationMetadata.Create(pageRequest.Page, pageRequest.PageSize, totalItems));
    }

    public async Task<ErrorOr<OrganizationResponse>> UpdateOrganizationAsync(
        Guid organizationId,
        Guid currentUserId,
        UpdateOrganizationRequest request,
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

        var slug = OrganizationSlug.Normalize(request.Slug);
        if (!string.Equals(slug, organization.Slug, StringComparison.Ordinal))
        {
            var slugTaken = await dbContext.Organizations
                .AnyAsync(o => o.Slug == slug && o.Id != organizationId, cancellationToken);
            if (slugTaken)
                return Error.Conflict(description: $"Organization slug '{slug}' already exists");
        }

        organization.UpdateDetails(request.Name, slug, request.Description);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(organization);
    }

    public async Task<ErrorOr<Success>> ArchiveOrganizationAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var organization = await dbContext.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken);

        if (organization is null)
            return Error.NotFound(description: "Organization not found");

        var membership = await FindMembershipAsync(organizationId, currentUserId, cancellationToken);

        var accessError = OrganizationAccess.EnsureMember(membership)
                          ?? (membership is null ? null : OrganizationAccess.EnsureOwner(membership))
                          ?? OrganizationAccess.EnsureActive(organization);
        if (accessError is not null) return accessError.Value;

        organization.Archive();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }

    public async Task<ErrorOr<OrganizationResponse>> UpdateLogoAsync(
        Guid organizationId,
        Guid currentUserId,
        Stream content,
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

        if (content.Length == 0)
            return Error.Validation(description: "Logo is required");

        var newKey = $"organizations/{organization.Id}/logo/{ObjectKeyGenerator.NewKey(".jpg")}";

        try
        {
            var uploadResult = await objectStorage.PutAsync(
                new PutObjectRequest
                {
                    Bucket = LogoBucket,
                    Key = newKey,
                    Content = content,
                    ContentType = "image/jpeg"
                },
                cancellationToken);

            var oldKey = organization.LogoKey;

            organization.SetLogo(uploadResult.Key);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // DB update failed -> remove the newly uploaded object.
                await objectStorage.DeleteAsync(LogoBucket, newKey, CancellationToken.None);
                throw;
            }

            // DB update succeeded -> remove the old object.
            if (!string.IsNullOrWhiteSpace(oldKey) &&
                !string.Equals(oldKey, newKey, StringComparison.Ordinal))
                try
                {
                    await objectStorage.DeleteAsync(
                        LogoBucket,
                        oldKey,
                        CancellationToken.None);
                }
                catch
                {
                    // Do not fail the request because the new logo is
                    // already persisted successfully.
                    logger.LogWarning(
                        "Failed to delete replaced organization logo object {OldLogoKey}",
                        oldKey);
                }

            return ToResponse(organization);
        }
        catch (ObjectStorageException)
        {
            return Error.Unexpected(description: "Failed to upload organization logo");
        }
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

    private static OrganizationResponse ToResponse(Organization organization)
    {
        return new OrganizationResponse(
            organization.Id,
            organization.Name,
            organization.Slug,
            organization.Description,
            organization.LogoKey,
            organization.Status.ToString(),
            organization.OwnerUserId,
            organization.CreatedAt,
            organization.UpdatedAt);
    }
}
