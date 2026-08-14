using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class ResendEmailConfirmationEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/resend-email-confirmation", ResendEmailConfirmationHandler)
            .WithValidation<ResendEmailConfirmationRequest>()
            .WithApiMetadata("Resend email confirmation", "Resends the email confirmation link.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ResendEmailConfirmationHandler(
        [FromBody] ResendEmailConfirmationRequest request,
        [FromServices] IAccountService accountService)
    {
        var result = await accountService.ResendEmailConfirmationAsync(request);
        return result.ToHttpResult();
    }
}

public class ResendEmailConfirmationRequestValidator : AbstractValidator<ResendEmailConfirmationRequest>
{
    public ResendEmailConfirmationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}