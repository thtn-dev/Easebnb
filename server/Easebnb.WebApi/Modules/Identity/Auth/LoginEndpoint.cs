using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Auth;

public sealed class LoginEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/auth";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/login", LoginHandler)
            .WithValidation<LoginRequest>()
            .WithApiMetadata("Login", "Authenticates user credentials and returns access tokens.")
            .WithStandardResponses<LoginResponse>();
    }

    private static async Task<IResult> LoginHandler(
        [FromBody] LoginRequest request,
        [FromServices] IAuthService authService,
        HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await authService.LoginAsync(request, ipAddress);
        return result.ToHttpResult();
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}