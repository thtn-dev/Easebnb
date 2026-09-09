using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Application;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;

namespace Easebnb.Organization.IntegrationTests.Organizations;

public class OrganizationMemberEndpointTests(OrganizationApiFixture fixture) : OrganizationApiTestBase(fixture)
{
    private string MembersUrl(Guid organizationId) => $"{OrganizationsUrl}/{organizationId}/members";

    // ---------------------------------------------------------------
    // GetMembers
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetMembers_WhenCallerIsMember_ReturnsProjectionEnrichedList()
    {
        // Arrange
        var (ownerClient, ownerLogin) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);

        var projectedMemberId = Guid.NewGuid();
        await SeedRegisteredUserAsync(projectedMemberId, "member@test.com", "member");
        await SeedMembershipAsync(organization.Id, projectedMemberId, OrganizationMemberRole.Member);

        var unprojectedMemberId = Guid.NewGuid();
        await SeedMembershipAsync(organization.Id, unprojectedMemberId, OrganizationMemberRole.Manager);

        // Act
        var response = await ownerClient.GetAsync($"{MembersUrl(organization.Id)}?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<OrganizationMemberResponse>>();
        page!.Data.Items.Should().HaveCount(3);
        page.Data.Pagination.TotalItems.Should().Be(3);

        var owner = page.Data.Items.Single(m => m.UserId == ownerLogin.User.Id);
        owner.Role.Should().Be("Owner");

        var projected = page.Data.Items.Single(m => m.UserId == projectedMemberId);
        projected.DisplayName.Should().Be("member");
        projected.Email.Should().Be("member@test.com");
        projected.Role.Should().Be("Member");
        projected.JoinedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));

        var unprojected = page.Data.Items.Single(m => m.UserId == unprojectedMemberId);
        unprojected.DisplayName.Should().BeNull();
        unprojected.Email.Should().BeNull();
    }

    [Fact]
    public async Task GetMembers_WhenSecondPageRequested_ReturnsPaginationMetadata()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        for (var i = 0; i < 3; i++)
        {
            var userId = Guid.NewGuid();
            await SeedRegisteredUserAsync(userId);
            await SeedMembershipAsync(organization.Id, userId, OrganizationMemberRole.Member);
        }

        // Act
        var response = await ownerClient.GetAsync($"{MembersUrl(organization.Id)}?page=2&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<OrganizationMemberResponse>>();
        page!.Data.Items.Should().HaveCount(2);
        page.Data.Pagination.CurrentPage.Should().Be(2);
        page.Data.Pagination.TotalItems.Should().Be(4);
        page.Data.Pagination.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetMembers_WhenCallerIsNotMember_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (outsiderClient, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await outsiderClient.GetAsync(MembersUrl(organization.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------
    // AddMember
    // ---------------------------------------------------------------

    [Fact]
    public async Task AddMember_WhenCalledByAdmin_Returns200WithProjectedUserInfo()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (adminClient, adminLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, adminLogin.User.Id, OrganizationMemberRole.Admin);

        var newMemberId = Guid.NewGuid();
        await SeedRegisteredUserAsync(newMemberId, "new-member@test.com", "new-member");

        // Act
        var response = await adminClient.PostAsJsonAsync(MembersUrl(organization.Id), new
        {
            userId = newMemberId,
            role = "Manager"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationMemberResponse>>();
        envelope!.Data!.UserId.Should().Be(newMemberId);
        envelope.Data.DisplayName.Should().Be("new-member");
        envelope.Data.Email.Should().Be("new-member@test.com");
        envelope.Data.Role.Should().Be("Manager");

        (await GetMembershipFromDbAsync(organization.Id, newMemberId))!.Role
            .Should().Be(OrganizationMemberRole.Manager);
    }

    [Fact]
    public async Task AddMember_WhenUserIsNotRegistered_Returns404()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);

        // Act
        var response = await ownerClient.PostAsJsonAsync(MembersUrl(organization.Id), new
        {
            userId = Guid.NewGuid(),
            role = "Member"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("User not found");
    }

    [Fact]
    public async Task AddMember_WhenUserIsAlreadyMember_Returns409()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var existingMemberId = Guid.NewGuid();
        await SeedRegisteredUserAsync(existingMemberId);
        await SeedMembershipAsync(organization.Id, existingMemberId, OrganizationMemberRole.Member);

        // Act
        var response = await ownerClient.PostAsJsonAsync(MembersUrl(organization.Id), new
        {
            userId = existingMemberId,
            role = "Admin"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddMember_WhenRoleIsOwner_Returns409()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var newMemberId = Guid.NewGuid();
        await SeedRegisteredUserAsync(newMemberId);

        // Act
        var response = await ownerClient.PostAsJsonAsync(MembersUrl(organization.Id), new
        {
            userId = newMemberId,
            role = "Owner"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetMembershipFromDbAsync(organization.Id, newMemberId)).Should().BeNull(
            "a second owner must never be created");
    }

    [Fact]
    public async Task AddMember_WhenRoleIsInvalid_Returns400ValidationProblem()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);

        // Act
        var response = await ownerClient.PostAsJsonAsync(MembersUrl(organization.Id), new
        {
            userId = Guid.NewGuid(),
            role = "SuperUser"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name)
            .Should().Contain("Role");
    }

    [Fact]
    public async Task AddMember_WhenCalledByMember_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (memberClient, memberLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, memberLogin.User.Id, OrganizationMemberRole.Member);
        var targetId = Guid.NewGuid();
        await SeedRegisteredUserAsync(targetId);

        // Act
        var response = await memberClient.PostAsJsonAsync(MembersUrl(organization.Id), new
        {
            userId = targetId,
            role = "Member"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetMembershipFromDbAsync(organization.Id, targetId)).Should().BeNull();
    }

    // ---------------------------------------------------------------
    // ChangeMemberRole
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeMemberRole_WhenOwnerGrantsOwnerRole_TransfersOwnership()
    {
        // Arrange
        var (ownerClient, ownerLogin) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var targetId = Guid.NewGuid();
        await SeedRegisteredUserAsync(targetId);
        await SeedMembershipAsync(organization.Id, targetId, OrganizationMemberRole.Member);

        // Act
        var response = await ownerClient.PutAsJsonAsync(
            $"{MembersUrl(organization.Id)}/{targetId}", new { role = "Owner" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationMemberResponse>>();
        envelope!.Data!.Role.Should().Be("Owner");

        (await GetMembershipFromDbAsync(organization.Id, targetId))!.Role
            .Should().Be(OrganizationMemberRole.Owner, "the target becomes the new owner");
        (await GetMembershipFromDbAsync(organization.Id, ownerLogin.User.Id))!.Role
            .Should().Be(OrganizationMemberRole.Admin, "the previous owner is demoted to admin");
        (await GetOrganizationFromDbAsync(organization.Id)).OwnerUserId
            .Should().Be(targetId, "the denormalized owner reference moves with the transfer");
    }

    [Fact]
    public async Task ChangeMemberRole_WhenAdminPromotesMember_Returns200()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (adminClient, adminLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, adminLogin.User.Id, OrganizationMemberRole.Admin);
        var targetId = Guid.NewGuid();
        await SeedRegisteredUserAsync(targetId);
        await SeedMembershipAsync(organization.Id, targetId, OrganizationMemberRole.Member);

        // Act
        var response = await adminClient.PutAsJsonAsync(
            $"{MembersUrl(organization.Id)}/{targetId}", new { role = "Manager" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetMembershipFromDbAsync(organization.Id, targetId))!.Role
            .Should().Be(OrganizationMemberRole.Manager);
    }

    [Fact]
    public async Task ChangeMemberRole_WhenAdminGrantsOwnerRole_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (adminClient, adminLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, adminLogin.User.Id, OrganizationMemberRole.Admin);
        var targetId = Guid.NewGuid();
        await SeedRegisteredUserAsync(targetId);
        await SeedMembershipAsync(organization.Id, targetId, OrganizationMemberRole.Member);

        // Act
        var response = await adminClient.PutAsJsonAsync(
            $"{MembersUrl(organization.Id)}/{targetId}", new { role = "Owner" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetOrganizationFromDbAsync(organization.Id)).OwnerUserId
            .Should().NotBe(targetId, "an admin must not be able to take over ownership");
    }

    [Fact]
    public async Task ChangeMemberRole_WhenTargetIsCurrentOwner_Returns409()
    {
        // Arrange
        var (ownerClient, ownerLogin) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);

        // Act
        var response = await ownerClient.PutAsJsonAsync(
            $"{MembersUrl(organization.Id)}/{ownerLogin.User.Id}", new { role = "Admin" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetMembershipFromDbAsync(organization.Id, ownerLogin.User.Id))!.Role
            .Should().Be(OrganizationMemberRole.Owner, "the owner must not be demoted directly");
    }

    [Fact]
    public async Task ChangeMemberRole_WhenOrganizationArchived_Returns409()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var targetId = Guid.NewGuid();
        await SeedRegisteredUserAsync(targetId);
        await SeedMembershipAsync(organization.Id, targetId, OrganizationMemberRole.Member);
        var archiveResponse = await ownerClient.PostAsync($"{OrganizationsUrl}/{organization.Id}/archive", null);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await ownerClient.PutAsJsonAsync(
            $"{MembersUrl(organization.Id)}/{targetId}", new { role = "Manager" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------------------------------------------------------------
    // RemoveMember
    // ---------------------------------------------------------------

    [Fact]
    public async Task RemoveMember_WhenCalledByOwner_RemovesMembership()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var targetId = Guid.NewGuid();
        await SeedRegisteredUserAsync(targetId);
        await SeedMembershipAsync(organization.Id, targetId, OrganizationMemberRole.Member);

        // Act
        var response = await ownerClient.DeleteAsync($"{MembersUrl(organization.Id)}/{targetId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetMembershipFromDbAsync(organization.Id, targetId)).Should().BeNull();
    }

    [Fact]
    public async Task RemoveMember_WhenTargetIsOwner_Returns409()
    {
        // Arrange
        var (ownerClient, ownerLogin) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);

        // Act
        var response = await ownerClient.DeleteAsync($"{MembersUrl(organization.Id)}/{ownerLogin.User.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await GetMembershipFromDbAsync(organization.Id, ownerLogin.User.Id)).Should().NotBeNull(
            "the last owner must never be removable");
    }

    [Fact]
    public async Task RemoveMember_WhenAdminRemovesAdmin_Returns403()
    {
        // Arrange
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);
        var (actorClient, actorLogin) = await CreateAuthorizedClientAsync();
        await SeedMembershipAsync(organization.Id, actorLogin.User.Id, OrganizationMemberRole.Admin);
        var otherAdminId = Guid.NewGuid();
        await SeedRegisteredUserAsync(otherAdminId);
        await SeedMembershipAsync(organization.Id, otherAdminId, OrganizationMemberRole.Admin);

        // Act
        var response = await actorClient.DeleteAsync($"{MembersUrl(organization.Id)}/{otherAdminId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await GetMembershipFromDbAsync(organization.Id, otherAdminId)).Should().NotBeNull();
    }
}
