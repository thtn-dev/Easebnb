using System.Net;

namespace Easebnb.Identity.IntegrationTests.Account;

public class ForgotPasswordEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string ForgotPasswordUrl = "/api/v1/account/forgot-password";

    [Fact]
    public async Task ForgotPassword_WithRegisteredEmail_Returns204()
    {
        // Arrange
        var user = await RegisterUserAsync();

        // Act
        var response = await PostJsonAsync(ForgotPasswordUrl, new { email = user.Email });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_StillReturns204()
    {
        // Act
        var response = await PostJsonAsync(ForgotPasswordUrl, new { email = "ghost@test.com" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "the API must not reveal whether an email is registered");
    }

    [Fact]
    public async Task ForgotPassword_WithInvalidEmail_Returns400ValidationProblem()
    {
        // Act
        var response = await PostJsonAsync(ForgotPasswordUrl, new { email = "not-an-email" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain("Email");
    }
}
