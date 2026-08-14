using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class ConfirmEmailEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/confirm-email", ConfirmEmailHandler)
            .WithValidation<ConfirmEmailRequest>()
            .WithApiMetadata("Confirm email", "Confirms the user's email address using a confirmation token.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ConfirmEmailHandler(
        [FromBody] ConfirmEmailRequest request,
        [FromServices] IAccountService accountService)
    {
        var result = await accountService.ConfirmEmailAsync(request);
        return result.ToHttpResult();
    }
}

public class ConfirmEmailRequestValidator : AbstractValidator<ConfirmEmailRequest>
{
    public ConfirmEmailRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Token)
            .NotEmpty();
    }
}