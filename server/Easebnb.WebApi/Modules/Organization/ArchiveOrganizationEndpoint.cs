using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class ArchiveOrganizationEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{organizationId:guid}/archive", ArchiveOrganizationHandler)
            .RequireAuthorization()
            .WithApiMetadata("Archive organization",
                "Archives the organization (Owner only). Archived organizations are read-only.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ArchiveOrganizationHandler(
        Guid organizationId,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await organizationService.ArchiveOrganizationAsync(organizationId, userId, cancellationToken);
        return result.ToHttpResult();
    }
}
