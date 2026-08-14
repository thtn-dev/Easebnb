using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class ChangePasswordEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/change-password", ChangePasswordHandler)
            .RequireAuthorization()
            .WithValidation<ChangePasswordRequest>()
            .WithApiMetadata("Change password", "Changes the password for the authenticated user.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ChangePasswordHandler(
        [FromBody] ChangePasswordRequest request,
        [FromServices] IAccountService accountService,
        [FromServices] ICurrentUserAccessor userAccessor)
    {
        var userId = userAccessor.GetRequiredCurrentUser().Id.Value;
        var result = await accountService.ChangePasswordAsync(userId, request);
        return result.ToHttpResult();
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(6);

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}