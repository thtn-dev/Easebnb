using BuildingBlocks.Endpoints.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;
using RefreshTokenRequest = Google.Apis.Auth.OAuth2.Requests.RefreshTokenRequest;

namespace Easebnb.WebApi.Modules.Identity.Auth;

public sealed class RefreshTokenEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/auth";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/refresh-token", RefreshTokenHandler)
            .WithValidation<RefreshTokenRequest>()
            .WithApiMetadata("Refresh token", "Issues a new access token from a valid refresh token.")
            .WithStandardResponses<LoginResponse>();
    }

    private static async Task<IResult> RefreshTokenHandler(
        [FromBody] RefreshTokenRequest request,
        [FromServices] IAuthService authService,
        HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await authService.RefreshTokenAsync(request.RefreshToken, ipAddress);
        return result.ToHttpResult();
    }
}

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty();
    }
}