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

public sealed class CreateOrganizationEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/organizations";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/", CreateOrganizationHandler)
            .RequireAuthorization()
            .WithValidation<CreateOrganizationRequest>()
            .WithApiMetadata("Create organization",
                "Creates a new organization. The current user becomes its owner.")
            .WithStandardResponses<OrganizationResponse>();
    }

    private static async Task<IResult> CreateOrganizationHandler(
        [FromBody] CreateOrganizationRequest request,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IOrganizationService organizationService,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await organizationService.CreateOrganizationAsync(userId, request, cancellationToken);
        return result.ToHttpResult();
    }
}

public class CreateOrganizationRequestValidator : AbstractValidator<CreateOrganizationRequest>
{
    public CreateOrganizationRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .MaximumLength(OrganizationSlug.MaxLength)
            .Must(slug => slug is null || OrganizationSlug.IsValid(slug))
            .WithMessage(
                "Slug must contain only lowercase letters, digits and hyphens, and cannot start or end with a hyphen.");

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
