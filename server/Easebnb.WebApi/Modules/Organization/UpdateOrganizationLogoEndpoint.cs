using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using BuildingBlocks.Infrastructure.FileUpload;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class UpdateOrganizationLogoEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{organizationId:guid}/logo", UpdateOrganizationLogoHandler)
            .DisableAntiforgery()
            .RequireAuthorization()
            .RequireRateLimiting("file-upload")
            .WithApiMetadata("Update organization logo",
                "Uploads a new logo image for the organization (Owner/Admin only).")
            .WithStandardResponses<OrganizationResponse>();
    }

    private static async Task<IResult> UpdateOrganizationLogoHandler(
        Guid organizationId,
        IFormFile file,
        HttpContext ctx,
        [FromServices] IOptions<OrganizationLogoUploadSettings> logoUploadSettings,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        CancellationToken ct)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        ctx.SetMaxBodySize(logoUploadSettings.Value.MaxFileSizeBytes + 1024 * 1024); // buffer multipart overhead
        var validation = await file.ValidateAsync(logoUploadSettings.Value.MaxFileSizeBytes,
            FileSignatures.ImageExtensions, ct);
        if (!validation.IsValid)
            return Results.BadRequest(new { error = validation.Error });

        await using var safeImage = await file.ToSafeJpegAsync(logoUploadSettings.Value.MaxDimension, ct: ct);
        if (safeImage is null)
            return Results.BadRequest(new { error = "File does not contain a valid image." });

        var result = await organizationService.UpdateLogoAsync(organizationId, userId, safeImage, ct);
        return result.ToHttpResult();
    }
}
