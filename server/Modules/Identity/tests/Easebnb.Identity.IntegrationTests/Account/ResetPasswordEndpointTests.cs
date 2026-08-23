using System.Net;

namespace Easebnb.Identity.IntegrationTests.Account;

public class ResetPasswordEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string ResetPasswordUrl = "/api/v1/account/reset-password";

    [Fact]
    public async Task ResetPassword_WithValidToken_Returns204AndNewPasswordWorks()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var token = await GeneratePasswordResetTokenAsync(user.Email);

        // Act
        var response = await PostJsonAsync(ResetPasswordUrl, new
        {
            email = user.Email,
            token,
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var newPasswordLogin = await PostJsonAsync(LoginUrl, new { username = user.Username, password = "NewPass123!" });
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK,
            "the new password must be usable immediately after the reset");
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        // Arrange
        var user = await RegisterUserAsync();

        // Act
        var response = await PostJsonAsync(ResetPasswordUrl, new
        {
            email = user.Email,
            token = EncodeToken("not-a-real-reset-token"),
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Contain("Password reset failed");
    }

    [Fact]
    public async Task ResetPassword_WithMismatchedConfirmation_Returns400ValidationProblem()
    {
        // Arrange — the endpoint validator rejects the mismatch before the service is reached
        var user = await RegisterUserAsync();
        var token = await GeneratePasswordResetTokenAsync(user.Email);

        // Act
        var response = await PostJsonAsync(ResetPasswordUrl, new
        {
            email = user.Email,
            token,
            newPassword = "NewPass123!",
            confirmNewPassword = "Different123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain("ConfirmNewPassword");
    }

    [Fact]
    public async Task ResetPassword_WithUnknownEmail_Returns400()
    {
        // Act
        var response = await PostJsonAsync(ResetPasswordUrl, new
        {
            email = "ghost@test.com",
            token = EncodeToken("any-token"),
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("Invalid request");
    }
}
