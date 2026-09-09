using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class GetOrganizationEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/{organizationId:guid}", GetOrganizationHandler)
            .RequireAuthorization()
            .WithApiMetadata("Get organization",
                "Gets an organization by id. The current user must be one of its members.")
            .WithStandardResponses<OrganizationResponse>();
    }

    private static async Task<IResult> GetOrganizationHandler(
        Guid organizationId,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await organizationService.GetOrganizationByIdAsync(organizationId, userId, cancellationToken);
        return result.ToHttpResult();
    }
}
