using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Application;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Organization.IntegrationTests.Organizations;

public class OrganizationEndpointTests(OrganizationApiFixture fixture) : OrganizationApiTestBase(fixture)
{
    // ---------------------------------------------------------------
    // Create
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_WhenAuthenticated_ReturnsOrganizationWithOwnerMembership()
    {
        // Arrange
        var (client, login) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(OrganizationsUrl, new
        {
            name = "Sơn Trà Hotel",
            slug = "son-tra-hotel",
            description = "A boutique hotel"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>();
        envelope!.Success.Should().BeTrue();
        var organization = envelope.Data!;
        organization.Name.Should().Be("Sơn Trà Hotel");
        organization.Slug.Should().Be("son-tra-hotel");
        organization.Status.Should().Be("Active");
        organization.OwnerUserId.Should().Be(login.User.Id, "the creator becomes the owner");

        var dbOrganization = await GetOrganizationFromDbAsync(organization.Id);
        dbOrganization.OwnerUserId.Should().Be(login.User.Id);
        dbOrganization.Status.Should().Be(OrganizationStatus.Active);

        var membership = await GetMembershipFromDbAsync(organization.Id, login.User.Id);
        membership.Should().NotBeNull("creating an organization must add the owner membership");
        membership!.Role.Should().Be(OrganizationMemberRole.Owner);
    }

    [Fact]
    public async Task Create_WhenSlugOmitted_GeneratesSlugFromVietnameseName()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(OrganizationsUrl, new { name = "Đại Dương Hotel" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>();
        envelope!.Data!.Slug.Should().Be("dai-duong-hotel");
    }

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        var response = await PostJsonAsync(OrganizationsUrl, new { name = "My Hotel" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_WhenSlugFormatInvalid_Returns400ValidationProblem()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(OrganizationsUrl, new
        {
            name = "My Hotel",
            slug = "Invalid Slug!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name)
            .Should().Contain("Slug");
    }

    [Fact]
    public async Task Create_WhenNameMissing_Returns400ValidationProblem()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(OrganizationsUrl, new { slug = "my-hotel" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name)
            .Should().Contain("Name");
    }

    [Fact]
    public async Task Create_WhenSlugAlreadyTaken_Returns409()
    {
        // Arrange
        var (firstClient, _) = await CreateAuthorizedClientAsync();
        await CreateOrganizationAsync(firstClient, slug: "taken-slug");

        var (secondClient, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await secondClient.PostAsJsonAsync(OrganizationsUrl, new
        {
            name = "Other Hotel",
            slug = "taken-slug"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Contain("already exists");
    }

    // ---------------------------------------------------------------
    // Get by id / slug
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetById_WhenCallerIsMember_Returns200()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client);

        // Act
        var response = await client.GetAsync($"{OrganizationsUrl}/{organization.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>();
        envelope!.Data!.Id.Should().Be(organization.Id);
    }

    [Fact]
    public async Task GetById_WhenCallerIsNotMember_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (outsiderClient, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await outsiderClient.GetAsync($"{OrganizationsUrl}/{organization.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_WhenOrganizationUnknown_Returns404()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.GetAsync($"{OrganizationsUrl}/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBySlug_WhenSlugProvidedWithDifferentCasing_Returns200()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client, slug: "son-tra-hotel");

        // Act
        var response = await client.GetAsync($"{OrganizationsUrl}/slug/SON-TRA-HOTEL");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>();
        envelope!.Data!.Id.Should().Be(organization.Id);
    }

    // ---------------------------------------------------------------
    // Get my organizations
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetMyOrganizations_ReturnsOnlyCallerOrganizationsWithPagination()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var first = await CreateOrganizationAsync(ownerClient, name: "First Hotel", slug: "first-hotel");
        var second = await CreateOrganizationAsync(ownerClient, name: "Second Hotel", slug: "second-hotel");

        var (otherClient, _) = await CreateAuthorizedClientAsync();
        await CreateOrganizationAsync(otherClient, name: "Someone Elses", slug: "someone-elses");

        // Act
        var response = await ownerClient.GetAsync($"{OrganizationsUrl}?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<OrganizationSummaryResponse>>();
        page!.Data.Items.Should().OnlyContain(o => o.Id == first.Id || o.Id == second.Id);
        page.Data.Items.Should().HaveCount(2);
        page.Data.Pagination.TotalItems.Should().Be(2);
        page.Data.Pagination.CurrentPage.Should().Be(1);
        var owned = page.Data.Items.Single(o => o.Id == first.Id);
        owned.Role.Should().Be("Owner");
        owned.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetMyOrganizations_WhenPageInvalid_Returns400()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.GetAsync($"{OrganizationsUrl}?page=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // Update
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_WhenCalledByOwner_Returns200AndPersistsDetails()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client);

        // Act
        var response = await client.PutAsJsonAsync($"{OrganizationsUrl}/{organization.Id}", new
        {
            name = "Renamed Hotel",
            slug = "renamed-hotel",
            description = "Updated description"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dbOrganization = await GetOrganizationFromDbAsync(organization.Id);
        dbOrganization.Name.Should().Be("Renamed Hotel");
        dbOrganization.Slug.Should().Be("renamed-hotel");
        dbOrganization.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task Update_WhenNewSlugTaken_Returns409()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client);
        await CreateOrganizationAsync(client, name: "Other", slug: "taken-slug");

        // Act
        var response = await client.PutAsJsonAsync($"{OrganizationsUrl}/{organization.Id}", new
        {
            name = "Renamed",
            slug = "taken-slug"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetOrganizationFromDbAsync(organization.Id)).Name
            .Should().Be("My Hotel", "the update must not be applied");
    }

    [Fact]
    public async Task Update_WhenCalledByMember_Returns403()
    {
        // Arrange
        var (ownerClient, ownerLogin) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (memberClient, memberLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, memberLogin.User.Id, OrganizationMemberRole.Member);

        // Act
        var response = await memberClient.PutAsJsonAsync($"{OrganizationsUrl}/{organization.Id}", new
        {
            name = "Hacked",
            slug = "hacked"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetOrganizationFromDbAsync(organization.Id)).Name.Should().Be("My Hotel");
        ownerLogin.User.Id.Should().NotBe(memberLogin.User.Id);
    }

    [Fact]
    public async Task Update_WhenOrganizationArchived_Returns409()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client);
        var archiveResponse = await client.PostAsync($"{OrganizationsUrl}/{organization.Id}/archive", null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await client.PutAsJsonAsync($"{OrganizationsUrl}/{organization.Id}", new
        {
            name = "Renamed",
            slug = "renamed"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("Organization is not active");
    }

    // ---------------------------------------------------------------
    // Archive
    // ---------------------------------------------------------------

    [Fact]
    public async Task Archive_WhenCalledByOwner_Returns204AndArchivesOrganization()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(client);

        // Act
        var response = await client.PostAsync($"{OrganizationsUrl}/{organization.Id}/archive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetOrganizationFromDbAsync(organization.Id)).Status
            .Should().Be(OrganizationStatus.Archived);
    }

    [Fact]
    public async Task Archive_WhenCalledByAdmin_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (adminClient, adminLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, adminLogin.User.Id, OrganizationMemberRole.Admin);

        // Act
        var response = await adminClient.PostAsync($"{OrganizationsUrl}/{organization.Id}/archive", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetOrganizationFromDbAsync(organization.Id)).Status
            .Should().Be(OrganizationStatus.Active);
    }
}
