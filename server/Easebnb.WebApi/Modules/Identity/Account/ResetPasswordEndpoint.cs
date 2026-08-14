using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class ResetPasswordEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/reset-password", ResetPasswordHandler)
            .WithValidation<ResetPasswordRequest>()
            .WithApiMetadata("Reset password", "Resets the user's password using a valid reset token.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ResetPasswordHandler(
        [FromBody] ResetPasswordRequest request,
        [FromServices] IAccountService accountService)
    {
        var result = await accountService.ResetPasswordAsync(request);
        return result.ToHttpResult();
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(6);

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}