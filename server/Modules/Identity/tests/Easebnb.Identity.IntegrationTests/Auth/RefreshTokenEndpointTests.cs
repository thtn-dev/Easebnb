using System.Net;
using Easebnb.Identity.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Identity.IntegrationTests.Auth;

public class RefreshTokenEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string RefreshUrl = "/api/v1/auth/refresh-token";

    [Fact]
    public async Task Refresh_WithValidToken_Returns200AndRotatesToken()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var login = await LoginAsync(user.Username, user.Password);

        // Act
        var response = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        var newAccessToken = data.GetProperty("accessToken").GetString();
        var newRefreshToken = data.GetProperty("refreshToken").GetString();
        newAccessToken.Should().NotBeNullOrEmpty();
        newRefreshToken.Should().NotBeNullOrEmpty().And.NotBe(login.RefreshToken,
            "refreshing must rotate the refresh token");

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        var oldToken = await db.RefreshTokens.AsNoTracking().SingleAsync(t => t.Token == login.RefreshToken);
        oldToken.IsRevoked.Should().BeTrue("using a refresh token must revoke it");
        oldToken.ReplacedByToken.Should().Be(newRefreshToken);
        var persistedNewToken = await db.RefreshTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Token == newRefreshToken);
        persistedNewToken.Should().NotBeNull("the rotated token must be persisted");
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        // Act
        var response = await PostJsonAsync(RefreshUrl, new { refreshToken = "does-not-exist" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_Returns401()
    {
        // Arrange — the first refresh revokes the login token
        var user = await RegisterUserAsync();
        var login = await LoginAsync(user.Username, user.Password);
        var firstRefresh = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — replaying the revoked token
        var response = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "a rotated refresh token must not be reusable");
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_Returns401()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var login = await LoginAsync(user.Username, user.Password);
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
            var token = await db.RefreshTokens.SingleAsync(t => t.Token == login.RefreshToken);
            token.ExpiresAt = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
        }

        // Act
        var response = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithEmptyToken_Returns400ValidationProblem()
    {
        // Act
        var response = await PostJsonAsync(RefreshUrl, new { refreshToken = "" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain("RefreshToken");
    }
}
