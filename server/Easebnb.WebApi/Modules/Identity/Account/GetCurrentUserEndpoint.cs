using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class GetCurrentUserEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/me", GetCurrentUserHandler)
            .RequireAuthorization()
            .WithApiMetadata("Get current user", "Gets information about the authenticated user.")
            .WithStandardResponses<UserInfo>();
    }

    private static async Task<IResult> GetCurrentUserHandler(
        [FromServices] IAuthService authService,
        [FromServices] ICurrentUserAccessor userAccessor)
    {
        var userId = userAccessor.GetRequiredCurrentUser().Id.Value;

        var userInfo = await authService.GetUserInfoAsync(userId);
        return userInfo.ToHttpResult();
    }
}