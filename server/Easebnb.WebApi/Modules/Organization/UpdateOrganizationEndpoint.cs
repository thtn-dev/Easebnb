using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Easebnb.Organization.Core.Common;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Easebnb.WebApi.Modules.Organization;

public sealed class UpdateOrganizationEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/{organizationId:guid}", UpdateOrganizationHandler)
            .RequireAuthorization()
            .WithValidation<UpdateOrganizationRequest>()
            .WithApiMetadata("Update organization",
                "Updates the organization's business details (Owner/Admin only).")
            .WithStandardResponses<OrganizationResponse>();
    }

    private static async Task<IResult> UpdateOrganizationHandler(
        Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await organizationService.UpdateOrganizationAsync(organizationId, userId, request, cancellationToken);
        return result.ToHttpResult();
    }
}

public class UpdateOrganizationRequestValidator : AbstractValidator<UpdateOrganizationRequest>
{
    public UpdateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(OrganizationSlug.MaxLength)
            .Must(OrganizationSlug.IsValid)
            .WithMessage(
                "Slug must contain only lowercase letters, digits and hyphens, and cannot start or end with a hyphen.");

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
