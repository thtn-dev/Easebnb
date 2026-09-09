using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class UpdateOrganizationMemberRoleEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/{organizationId:guid}/members/{userId:guid}", UpdateOrganizationMemberRoleHandler)
            .RequireAuthorization()
            .WithValidation<UpdateOrganizationMemberRoleRequest>()
            .WithApiMetadata("Update organization member role",
                "Changes a member's role. Granting the Owner role transfers ownership (Owner only).")
            .WithStandardResponses<OrganizationMemberResponse>();
    }

    private static async Task<IResult> UpdateOrganizationMemberRoleHandler(
        Guid organizationId,
        Guid userId,
        [FromBody] UpdateOrganizationMemberRoleRequest request,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationMemberService memberService,
        CancellationToken cancellationToken)
    {
        var currentUserId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await memberService.ChangeMemberRoleAsync(
            organizationId, currentUserId, userId, request, cancellationToken);
        return result.ToHttpResult();
    }
}

public class UpdateOrganizationMemberRoleRequestValidator : AbstractValidator<UpdateOrganizationMemberRoleRequest>
{
    public UpdateOrganizationMemberRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<OrganizationMemberRole>(role, ignoreCase: true, out var parsed)
                          && Enum.IsDefined(parsed))
            .WithMessage("Role must be one of: Owner, Admin, Manager, Member.");
    }
}
