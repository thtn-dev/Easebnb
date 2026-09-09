using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;

namespace Easebnb.Organization.IntegrationTests.Organizations;

public class OrganizationLogoEndpointTests(OrganizationApiFixture fixture) : OrganizationApiTestBase(fixture)
{
    // Note: the "file-upload" rate limiter is a global fixed window of 10
    // requests/minute, so this suite keeps handler-reaching uploads low.

    [Fact]
    public async Task UploadLogo_WhenCalledByAdmin_UploadsPersistsKeyAndReplacesOldLogo()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (adminClient, adminLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, adminLogin.User.Id, OrganizationMemberRole.Admin);
        SetupSuccessfulLogoUpload();

        using var firstUpload = await adminClient.PostAsync(
            $"{OrganizationsUrl}/{organization.Id}/logo", CreateLogoContent(GenerateJpegBytes()));
        firstUpload.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstKey = (await firstUpload.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>())!.Data!.LogoKey;
        firstKey.Should().NotBeNullOrEmpty();

        // Act — a second upload replaces the first logo
        using var secondUpload = await adminClient.PostAsync(
            $"{OrganizationsUrl}/{organization.Id}/logo", CreateLogoContent(GenerateJpegBytes()));

        // Assert
        secondUpload.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await secondUpload.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>();
        var newKey = envelope!.Data!.LogoKey;
        newKey.Should().NotBeNullOrEmpty().And.NotBe(firstKey);

        (await GetOrganizationFromDbAsync(organization.Id)).LogoKey.Should().Be(newKey);

        Fixture.ObjectStorageMock.Verify(
            s => s.PutAsync(
                It.Is<PutObjectRequest>(r =>
                    r.Bucket == "easebnb-organizations" &&
                    r.Key.StartsWith($"organizations/{organization.Id}/logo/")),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Fixture.ObjectStorageMock.Verify(
            s => s.DeleteAsync("easebnb-organizations", firstKey!, It.IsAny<CancellationToken>()),
            Times.Once, "the replaced logo object must be deleted");
    }

    [Fact]
    public async Task UploadLogo_WhenFileIsNotAnImage_Returns400()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client);
        SetupSuccessfulLogoUpload();

        // Act — magic bytes do not match the .jpg signature
        using var content = CreateLogoContent("this is not an image"u8.ToArray());
        using var response = await client.PostAsync($"{OrganizationsUrl}/{organization.Id}/logo", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Fixture.ObjectStorageMock.Verify(
            s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        (await GetOrganizationFromDbAsync(organization.Id)).LogoKey.Should().BeNull();
    }

    [Fact]
    public async Task UploadLogo_WhenCalledByMember_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (memberClient, memberLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, memberLogin.User.Id, OrganizationMemberRole.Member);
        SetupSuccessfulLogoUpload();

        // Act
        using var response = await memberClient.PostAsync(
            $"{OrganizationsUrl}/{organization.Id}/logo", CreateLogoContent(GenerateJpegBytes()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        Fixture.ObjectStorageMock.Verify(
            s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UploadLogo_WithoutToken_Returns401()
    {
        // Arrange
        SetupSuccessfulLogoUpload();

        // Act
        using var response = await Client.PostAsync(
            $"{OrganizationsUrl}/{Guid.NewGuid()}/logo", CreateLogoContent(GenerateJpegBytes()));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
