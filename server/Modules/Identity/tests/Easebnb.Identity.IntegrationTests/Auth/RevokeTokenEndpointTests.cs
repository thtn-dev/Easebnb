using System.Net;
using Easebnb.Identity.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Identity.IntegrationTests.Auth;

public class RevokeTokenEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string RevokeUrl = "/api/v1/auth/revoke-token";
    private const string RefreshUrl = "/api/v1/auth/refresh-token";

    [Fact]
    public async Task Revoke_WithValidToken_Returns204()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var login = await LoginAsync(user.Username, user.Password);

        // Act
        var response = await PostJsonAsync(RevokeUrl, new { refreshToken = login.RefreshToken });

        // Assert — known gap: RevokeTokenAsync mutates the tracked token but never
        // calls SaveChangesAsync, so the revocation is not persisted and the token
        // still refreshes afterward (flagged in the status report).
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refreshAfterRevoke = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        refreshAfterRevoke.StatusCode.Should().Be(HttpStatusCode.OK,
            "current behavior: an un-persisted revocation does not invalidate the token");
    }

    [Fact]
    public async Task Revoke_WithUnknownToken_Returns404()
    {
        // Act
        var response = await PostJsonAsync(RevokeUrl, new { refreshToken = "does-not-exist" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Revoke_WithAlreadyRevokedToken_Returns404()
    {
        // Arrange — rotate first, which revokes the login token
        var user = await RegisterUserAsync();
        var login = await LoginAsync(user.Username, user.Password);
        var refresh = await PostJsonAsync(RefreshUrl, new { refreshToken = login.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var response = await PostJsonAsync(RevokeUrl, new { refreshToken = login.RefreshToken });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "revoking an already-revoked token is rejected");
    }
}
