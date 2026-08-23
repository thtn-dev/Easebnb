using System.Net;
using System.Net.Http.Json;
using BuildingBlocks.Application;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Organization.IntegrationTests.IntegrationEvents;

/// <summary>
///     End-to-end coverage of the cross-module pipeline behind organization
///     membership: registering a user in the Identity module raises
///     UserRegisteredDomainEvent -> MassTransit EF outbox (identity schema) ->
///     in-memory transport -> UserRegisteredConsumer with EF inbox
///     (organization schema) -> registered_users projection, which the
///     add-member business rule depends on.
/// </summary>
public class UserRegisteredProjectionTests(OrganizationApiFixture fixture) : OrganizationApiTestBase(fixture)
{
    [Fact]
    public async Task RegisterUser_ThenProjectionAppearsAndUserCanBeAddedAsMember()
    {
        // Arrange — an organization whose owner will add the new user
        var (ownerClient, _) = await CreateAuthorizedClientAsync();
        var organization = await CreateOrganizationAsync(ownerClient);

        // Act 1 — register a brand-new user through the Identity API
        var newUser = await RegisterUserAsync();
        var (newUserClient, newUserLogin) = await LoginAsAsync(newUser);

        // Assert 1 — the projection eventually appears (outbox delivery ~2s)
        var projected = await WaitForRegisteredUserAsync(newUserLogin.User.Id);
        projected.Email.Should().Be(newUser.Email);
        projected.UserName.Should().Be(newUser.Username);

        // Act 2 — the projected user satisfies the "user must exist" rule
        var addResponse = await ownerClient.PostAsJsonAsync(
            $"{OrganizationsUrl}/{organization.Id}/members",
            new { userId = newUserLogin.User.Id, role = "Member" });

        // Assert 2 — member added, listing shows the real display info
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "the registered-user projection must make the user addable as a member");
        var membersResponse = await ownerClient.GetAsync($"{OrganizationsUrl}/{organization.Id}/members");
        membersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await membersResponse.Content
            .ReadFromJsonAsync<PaginatedResponse<OrganizationMemberResponse>>();
        var added = page!.Data.Items.Single(m => m.UserId == newUserLogin.User.Id);
        added.DisplayName.Should().Be(newUser.Username,
            "display fields come from the projection filled by the integration event, not manual seeding");
        added.Email.Should().Be(newUser.Email);
        added.Role.Should().Be("Member");

        // The new member can now see the organization (membership gate opens).
        var visibleResponse = await newUserClient.GetAsync($"{OrganizationsUrl}/{organization.Id}");
        visibleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterUser_WhenProjectionConsumed_InboxRecordsTheMessageId()
    {
        // The inbox_state row proves the consumer ran through the EF outbox/
        // inbox pipeline (not, say, a direct database side effect).
        var user = await RegisterUserAsync();
        var (_, login) = await LoginAsAsync(user);
        await WaitForRegisteredUserAsync(login.User.Id);

        await using var scope = Fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM organization.inbox_state";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync());

        count.Should().BeGreaterThan(0,
            "the consumer's inbox must record consumed MessageIds for at-least-once dedup");
    }
}
