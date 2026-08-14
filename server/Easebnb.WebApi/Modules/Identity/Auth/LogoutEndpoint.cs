using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.WebApi.Extensions;

namespace Easebnb.WebApi.Modules.Identity.Auth;

public sealed class LogoutEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/auth";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/logout", LogoutHandler)
            .RequireAuthorization()
            .WithApiMetadata("Logout", "Revokes all active refresh tokens for the current user.")
            .Produces<ApiResponse>()
            .WithStandardResponses();
    }

    private static async Task<IResult> LogoutHandler(
        [FromServices] AppIdentityDbContext dbContext,
        [FromServices] ICurrentUserAccessor userAccessor,
        HttpContext httpContext)
    {
        var userId = userAccessor.GetRequiredCurrentUser().Id.Value;

        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.IsRevoked == false && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
        }

        await dbContext.SaveChangesAsync();
        return Results.Ok(ApiResponse.Ok("Logged out successfully"));
    }
}