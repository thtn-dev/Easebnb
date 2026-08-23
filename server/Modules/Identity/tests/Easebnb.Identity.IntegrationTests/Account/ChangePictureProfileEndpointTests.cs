using System.Net;
using System.Net.Http.Headers;
using BuildingBlocks.Application.ObjectStorage.Abstractions;

namespace Easebnb.Identity.IntegrationTests.Account;

public class ChangePictureProfileEndpointTests : IdentityApiTestBase
{
    private const string ChangePictureUrl = "/api/v1/account/change-picture-profile";

    /// <summary>A minimal valid 1×1 PNG (signature, IHDR, IDAT, IEND).</summary>
    private static byte[] PngBytes =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE,
        0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54,
        0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00, 0x00,
        0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0,
        0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    public ChangePictureProfileEndpointTests(IdentityApiFixture fixture) : base(fixture)
    {
        Fixture.ObjectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PutObjectRequest request, CancellationToken _) => new PutObjectResult
            {
                Bucket = request.Bucket,
                Key = request.Key,
                ETag = "\"test-etag\""
            });
    }

    private static MultipartFormDataContent CreatePictureContent(byte[] bytes)
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", "avatar.png");
        return content;
    }

    [Fact]
    public async Task ChangePicture_WithValidPng_Returns204AndUploadsToStorage()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);
        var (client, _) = await LoginAsAsync(user);
        PutObjectRequest? captured = null;
        Fixture.ObjectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync((PutObjectRequest request, CancellationToken _) => new PutObjectResult
            {
                Bucket = request.Bucket,
                Key = request.Key,
                ETag = "\"test-etag\""
            });

        // Act
        var response = await client.PostAsync(ChangePictureUrl, CreatePictureContent(PngBytes));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        captured.Should().NotBeNull();
        captured!.Bucket.Should().Be("easebnb-users");
        captured.Key.Should().StartWith($"users/{persisted.Id}/profile-picture/");
        (await GetUserByUsernameAsync(user.Username)).ProfilePictureKey.Should().Be(captured.Key,
            "the uploaded key must be persisted on the user");
    }

    [Fact]
    public async Task ChangePicture_WithNonImageContent_Returns400()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsync(ChangePictureUrl, CreatePictureContent("this is not an image"u8.ToArray()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("error").GetString().Should().NotBeNullOrEmpty();
        Fixture.ObjectStorageMock.Verify(
            s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "an invalid file must never reach object storage");
    }

    [Fact]
    public async Task ChangePicture_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.PostAsync(ChangePictureUrl, CreatePictureContent(PngBytes));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
