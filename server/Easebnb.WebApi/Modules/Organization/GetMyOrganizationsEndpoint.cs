using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class GetMyOrganizationsEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/", GetMyOrganizationsHandler)
            .RequireAuthorization()
            .WithApiMetadata("Get my organizations",
                "Lists the organizations the current user belongs to, paginated.")
            .WithPaginatedResponses<OrganizationSummaryResponse>();
    }

    private static async Task<IResult> GetMyOrganizationsHandler(
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!EndpointExtensions.ValidatePagination(page, pageSize, out var errorResult))
            return errorResult!;

        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await organizationService.GetMyOrganizationsAsync(
            userId,
            new PagedRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        if (result.IsError) return result.ToHttpResult();
        return Results.Ok(result.Value);
    }
}
