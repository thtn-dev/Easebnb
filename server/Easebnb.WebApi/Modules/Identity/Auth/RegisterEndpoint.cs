using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Auth;

public sealed class RegisterEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/auth";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/register", RegisterHandler)
            .WithValidation<RegisterRequest>()
            .WithApiMetadata("Register user", "Creates a new user account.")
            .WithStandardResponses<LoginResponse>();
    }

    private static async Task<IResult> RegisterHandler(
        [FromBody] RegisterRequest request,
        [FromServices] IAuthService authService)
    {
        var result = await authService.RegisterAsync(request);
        return result.ToHttpResult();
    }
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(6);

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password)
            .WithMessage("Passwords do not match.");
    }
}