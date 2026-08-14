using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class ForgotPasswordEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/forgot-password", ForgotPasswordHandler)
            .WithValidation<ForgotPasswordRequest>()
            .WithApiMetadata("Forgot password", "Sends a password reset link to the user's email.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ForgotPasswordHandler(
        [FromBody] ForgotPasswordRequest request,
        [FromServices] IAccountService accountService)
    {
        var result = await accountService.ForgotPasswordAsync(request);
        return result.ToHttpResult();
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}