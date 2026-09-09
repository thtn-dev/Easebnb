using System.Net;
using Easebnb.Identity.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Identity.IntegrationTests.Auth;

public class LogoutEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string LogoutUrl = "/api/v1/auth/logout";
    private const string RefreshUrl = "/api/v1/auth/refresh-token";

    [Fact]
    public async Task Logout_WithBearerToken_Returns200AndRevokesAllRefreshTokens()
    {
        // Arrange — two active refresh tokens (one direct login, one rotated)
        var user = await RegisterUserAsync();
        var login = await LoginAsync(user.Username, user.Password);
        var refresh = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondLogin = await LoginAsync(user.Username, user.Password);
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", secondLogin.AccessToken);

        // Act
        var response = await client.PostAsync(LogoutUrl, null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await ReadJsonAsync(response);
        body.GetProperty("success").GetBoolean().Should().BeTrue();
        body.GetProperty("message").GetString().Should().Be("Logged out successfully");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var tokens = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == secondLogin.User.Id)
            .ToListAsync();
        tokens.Should().NotBeEmpty();
        tokens.Should().OnlyContain(t => t.IsRevoked, "logout must revoke every active refresh token");
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        // Act
        var response = await Client.PostAsync(LogoutUrl, null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
