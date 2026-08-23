using System.Net;

namespace Easebnb.Identity.IntegrationTests.Auth;

public class LoginEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndUser()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);

        // Act
        var response = await PostJsonAsync(LoginUrl, new { username = user.Username, password = user.Password });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        var data = body.GetProperty("data");
        data.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("tokenType").GetString().Should().Be("Bearer");
        data.GetProperty("expiresIn").GetInt32().Should().Be(3600);
        var userInfo = data.GetProperty("user");
        userInfo.GetProperty("id").GetGuid().Should().Be(persisted.Id);
        userInfo.GetProperty("username").GetString().Should().Be(user.Username);
        userInfo.GetProperty("email").GetString().Should().Be(user.Email);
        userInfo.GetProperty("emailConfirmed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithUnknownUsername_Returns401()
    {
        // Act
        var response = await PostJsonAsync(LoginUrl, new { username = "ghost", password = "Pass123!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        // Arrange
        var user = await RegisterUserAsync();

        // Act
        var response = await PostJsonAsync(LoginUrl, new { username = user.Username, password = "WrongPass1!" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_Returns403ForLockout()
    {
        // Arrange — the first four failures are plain 401s
        var user = await RegisterUserAsync();
        for (var i = 0; i < 4; i++)
        {
            var failed = await PostJsonAsync(LoginUrl, new { username = user.Username, password = "WrongPass1!" });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"attempt {i + 1} should just fail");
        }

        // Act — the fifth failure locks the account
        var fifthAttempt = await PostJsonAsync(LoginUrl, new { username = user.Username, password = "WrongPass1!" });
        var correctWhileLocked = await PostJsonAsync(LoginUrl, new { username = user.Username, password = user.Password });

        // Assert
        fifthAttempt.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "the fifth consecutive failure locks the account");
        correctWhileLocked.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "even the correct password is rejected while locked out");
    }

    [Fact]
    public async Task Login_WithEmptyBody_Returns400ValidationProblem()
    {
        // Act
        var response = await PostJsonAsync(LoginUrl, new { username = "", password = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain(["Username", "Password"]);
    }

    [Fact]
    public async Task Login_ReturnedAccessToken_AuthorizesProtectedEndpoints()
    {
        // Arrange
        var (client, login) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.GetAsync("/api/v1/account/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the access token issued by login must be accepted by [Authorize] endpoints");
        login.AccessToken.Should().NotBeNullOrEmpty();
    }
}
