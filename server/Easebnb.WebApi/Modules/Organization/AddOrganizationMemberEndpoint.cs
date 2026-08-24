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

public sealed class AddOrganizationMemberEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/{organizationId:guid}/members", AddOrganizationMemberHandler)
            .RequireAuthorization()
            .WithValidation<AddOrganizationMemberRequest>()
            .WithApiMetadata("Add organization member",
                "Adds an existing user to the organization with the given role (Owner/Admin only).")
            .WithStandardResponses<OrganizationMemberResponse>();
    }

    private static async Task<IResult> AddOrganizationMemberHandler(
        Guid organizationId,
        [FromBody] AddOrganizationMemberRequest request,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationMemberService memberService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await memberService.AddMemberAsync(organizationId, userId, request, cancellationToken);
        return result.ToHttpResult();
    }
}

public class AddOrganizationMemberRequestValidator : AbstractValidator<AddOrganizationMemberRequest>
{
    public AddOrganizationMemberRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => Enum.TryParse<OrganizationMemberRole>(role, ignoreCase: true, out var parsed)
                          && Enum.IsDefined(parsed))
            .WithMessage("Role must be one of: Owner, Admin, Manager, Member.");
    }
}
