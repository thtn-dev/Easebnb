using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class UpdateProfileEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPut("/me", UpdateProfileHandler)
            .RequireAuthorization()
            .WithValidation<UpdateProfileRequest>()
            .WithApiMetadata("Update profile", "Updates the authenticated user's profile information.")
            .WithStandardResponses<UserInfo>();
    }

    private static async Task<IResult> UpdateProfileHandler(
        [FromBody] UpdateProfileRequest request,
        [FromServices] IAccountService accountService,
        [FromServices] ICurrentUserAccessor userAccessor)
    {
        var userId = userAccessor.GetRequiredCurrentUser().Id.Value;

        var result = await accountService.UpdateProfileAsync(userId, request);
        return result.ToHttpResult();
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(20)
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}