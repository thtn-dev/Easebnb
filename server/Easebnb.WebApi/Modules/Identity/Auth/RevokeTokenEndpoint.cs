using BuildingBlocks.Endpoints.Abstractions;
using Google.Apis.Auth.OAuth2.Requests;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Easebnb.WebApi.Filters;

namespace Easebnb.WebApi.Modules.Identity.Auth;

public sealed class RevokeTokenEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/auth";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/revoke-token", RevokeTokenHandler)
            .WithValidation<RefreshTokenRequest>()
            .WithApiMetadata("Revoke token", "Revokes a refresh token.")
            .WithStandardResponses();
    }

    private static async Task<IResult> RevokeTokenHandler(
        [FromBody] RefreshTokenRequest request,
        [FromServices] IAuthService authService,
        HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await authService.RevokeTokenAsync(request.RefreshToken, ipAddress);
        return result.ToHttpResult();
    }
}