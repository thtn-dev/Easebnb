namespace Easebnb.Organization.Core.Dtos;

public record CreateOrganizationRequest(
    string Name,
    string? Slug,
    string? Description);

public record UpdateOrganizationRequest(
    string Name,
    string Slug,
    string? Description);

public record OrganizationResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string? LogoKey,
    string Status,
    Guid OwnerUserId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record OrganizationSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? LogoKey,
    string Status,
    string Role,
    DateTime CreatedAt);

public record OrganizationMemberResponse(
    Guid UserId,
    string? DisplayName,
    string? Email,
    string Role,
    DateTime JoinedAt);

public record AddOrganizationMemberRequest(
    Guid UserId,
    string Role);

public record UpdateOrganizationMemberRoleRequest(
    string Role);
