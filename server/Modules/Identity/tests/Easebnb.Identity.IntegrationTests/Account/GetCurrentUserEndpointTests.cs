using System.Net;

namespace Easebnb.Identity.IntegrationTests.Account;

public class GetCurrentUserEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string MeUrl = "/api/v1/account/me";

    [Fact]
    public async Task GetCurrentUser_WithBearerToken_ReturnsUserInfo()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);
        var (client, _) = await LoginAsAsync(user);

        // Act
        var response = await client.GetAsync(MeUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = body.GetProperty("data");
        data.GetProperty("id").GetGuid().Should().Be(persisted.Id);
        data.GetProperty("username").GetString().Should().Be(user.Username);
        data.GetProperty("email").GetString().Should().Be(user.Email);
        data.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentUser_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.GetAsync(MeUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
