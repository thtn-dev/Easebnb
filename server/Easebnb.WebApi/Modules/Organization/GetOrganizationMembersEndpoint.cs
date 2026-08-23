using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class GetOrganizationMembersEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/{organizationId:guid}/members", GetOrganizationMembersHandler)
            .RequireAuthorization()
            .WithApiMetadata("Get organization members",
                "Lists the members of an organization, paginated. Any member can view the list.")
            .WithPaginatedResponses<OrganizationMemberResponse>();
    }

    private static async Task<IResult> GetOrganizationMembersHandler(
        Guid organizationId,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationMemberService memberService,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!EndpointExtensions.ValidatePagination(page, pageSize, out var errorResult))
            return errorResult!;

        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await memberService.GetMembersAsync(
            organizationId,
            userId,
            new PagedRequest { Page = page, PageSize = pageSize },
            cancellationToken);

        if (result.IsError) return result.ToHttpResult();
        return Results.Ok(result.Value);
    }
}
