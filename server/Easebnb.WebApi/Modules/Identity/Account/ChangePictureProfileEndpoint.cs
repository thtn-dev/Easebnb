using BuildingBlocks.Application;
using BuildingBlocks.Endpoints.Abstractions;
using BuildingBlocks.Infrastructure.FileUpload;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Easebnb.WebApi.Modules.Identity.Account;

public sealed class ChangePictureProfileEndpoint : IEndpointGroup
{
    public string GroupPrefix => "/api/v1/account";

    public void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/change-picture-profile", ChangePictureProfileHandler)
            .DisableAntiforgery()
            .RequireAuthorization()
            .RequireRateLimiting("file-upload")
            .WithApiMetadata("Change profile picture", "Changes the user's profile picture.")
            .WithStandardResponses();
    }

    private static async Task<IResult> ChangePictureProfileHandler(
        IFormFile file,
        HttpContext ctx,
        [FromServices] IOptions<AvatarUploadSettings> avatarUploadSettings,
        [FromServices] ICurrentUserAccessor currentUserAccessor,
        [FromServices] IAccountService accountService, CancellationToken ct)
    {
        var userId = currentUserAccessor.GetRequiredCurrentUser().Id;

        ctx.SetMaxBodySize(avatarUploadSettings.Value.MaxFileSizeBytes + 1024 * 1024); // buffer multipart overhead
        var validation = await file.ValidateAsync(avatarUploadSettings.Value.MaxFileSizeBytes,
            FileSignatures.ImageExtensions, ct);
        if (!validation.IsValid)
            return Results.BadRequest(new { error = validation.Error });

        await using var safeImage = await file.ToSafeJpegAsync(avatarUploadSettings.Value.MaxDimension, ct: ct);
        if (safeImage is null)
            return Results.BadRequest(new { error = "File does not contain a valid image." });

        var contentType = file.ContentType;
        var request = new UpdateProfilePictureRequest(safeImage, contentType);
        var result = await accountService.UpdateProfilePictureAsync(userId.Value, request);
        return result.ToHttpResult();
    }
}