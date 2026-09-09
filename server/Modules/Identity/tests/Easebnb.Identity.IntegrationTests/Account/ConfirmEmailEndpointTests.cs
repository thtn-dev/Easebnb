using System.Net;

namespace Easebnb.Identity.IntegrationTests.Account;

public class ConfirmEmailEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string ConfirmEmailUrl = "/api/v1/account/confirm-email";

    [Fact]
    public async Task ConfirmEmail_WithValidToken_Returns204AndConfirmsUser()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);
        var token = await GenerateEmailConfirmationTokenAsync(persisted.Id);

        // Act
        var response = await PostJsonAsync(ConfirmEmailUrl, new { userId = persisted.Id, token });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetUserByUsernameAsync(user.Username)).EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmail_WithInvalidToken_Returns400()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);
        var token = EncodeToken("not-a-real-confirmation-token");

        // Act
        var response = await PostJsonAsync(ConfirmEmailUrl, new { userId = persisted.Id, token });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Contain("Email confirmation failed");
        (await GetUserByUsernameAsync(user.Username)).EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmEmail_WithUnknownUser_Returns400()
    {
        // Act
        var response = await PostJsonAsync(ConfirmEmailUrl, new
        {
            userId = Guid.NewGuid(),
            token = EncodeToken("any-token")
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("Invalid request");
    }
}
