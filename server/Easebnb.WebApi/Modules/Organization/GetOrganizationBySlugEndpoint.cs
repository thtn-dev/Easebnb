using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class GetOrganizationBySlugEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/slug/{slug}", GetOrganizationBySlugHandler)
            .RequireAuthorization()
            .WithApiMetadata("Get organization by slug",
                "Gets an organization by its slug. The current user must be one of its members.")
            .WithStandardResponses<OrganizationResponse>();
    }

    private static async Task<IResult> GetOrganizationBySlugHandler(
        string slug,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await organizationService.GetOrganizationBySlugAsync(slug, userId, cancellationToken);
        return result.ToHttpResult();
    }
}
