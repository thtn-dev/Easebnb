using System.Net;

namespace Easebnb.Identity.IntegrationTests.Account;

public class ResendEmailConfirmationEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string ResendUrl = "/api/v1/account/resend-email-confirmation";

    [Fact]
    public async Task Resend_WithUnconfirmedEmail_Returns204()
    {
        // Arrange
        var user = await RegisterUserAsync();

        // Act
        var response = await PostJsonAsync(ResendUrl, new { email = user.Email });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Resend_WithAlreadyConfirmedEmail_Returns400()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);
        var token = await GenerateEmailConfirmationTokenAsync(persisted.Id);
        var confirm = await PostJsonAsync("/api/v1/account/confirm-email", new { userId = persisted.Id, token });
        confirm.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act
        var response = await PostJsonAsync(ResendUrl, new { email = user.Email });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("Email is already confirmed");
    }

    [Fact]
    public async Task Resend_WithUnknownEmail_StillReturns204()
    {
        // Act
        var response = await PostJsonAsync(ResendUrl, new { email = "ghost@test.com" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "the API must not reveal whether an email is registered");
    }
}
