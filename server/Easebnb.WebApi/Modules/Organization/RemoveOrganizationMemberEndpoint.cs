using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class RemoveOrganizationMemberEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapDelete("/{organizationId:guid}/members/{userId:guid}", RemoveOrganizationMemberHandler)
            .RequireAuthorization()
            .WithApiMetadata("Remove organization member",
                "Removes a member from the organization (Owner/Admin only). The owner cannot be removed.")
            .WithStandardResponses();
    }

    private static async Task<IResult> RemoveOrganizationMemberHandler(
        Guid organizationId,
        Guid userId,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationMemberService memberService,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await memberService.RemoveMemberAsync(organizationId, currentUserId, userId, cancellationToken);
        return result.ToHttpResult();
    }
}
