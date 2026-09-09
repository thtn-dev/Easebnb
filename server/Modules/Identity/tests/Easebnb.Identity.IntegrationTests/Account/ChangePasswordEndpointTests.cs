using System.Net;
using System.Net.Http.Json;

namespace Easebnb.Identity.IntegrationTests.Account;

public class ChangePasswordEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string ChangePasswordUrl = "/api/v1/account/change-password";

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Returns204AndRotatesPassword()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var (client, _) = await LoginAsAsync(user);

        // Act
        var response = await client.PostAsJsonAsync(ChangePasswordUrl, new
        {
            currentPassword = user.Password,
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var oldPasswordLogin = await PostJsonAsync(LoginUrl, new { username = user.Username, password = user.Password });
        oldPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the old password must stop working after a change");

        var newPasswordLogin = await PostJsonAsync(LoginUrl, new { username = user.Username, password = "NewPass123!" });
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(ChangePasswordUrl, new
        {
            currentPassword = "WrongPass1!",
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Contain("Password change failed");
    }

    [Fact]
    public async Task ChangePassword_WithMismatchedConfirmation_Returns400ValidationProblem()
    {
        // Arrange — the endpoint validator rejects the mismatch before the
        // service is reached (its Error.Unexpected path stays untested here).
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PostAsJsonAsync(ChangePasswordUrl, new
        {
            currentPassword = "Pass123!",
            newPassword = "NewPass123!",
            confirmNewPassword = "Different123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain("ConfirmNewPassword");
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        // Act
        var response = await PostJsonAsync(ChangePasswordUrl, new
        {
            currentPassword = "Pass123!",
            newPassword = "NewPass123!",
            confirmNewPassword = "NewPass123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
