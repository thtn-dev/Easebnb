using System.Net;
using System.Security.Cryptography;

namespace Easebnb.Identity.IntegrationTests.Auth;

public class JwksEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Jwks_ReturnsRsaPublicKeyWithExpectedKeyId()
    {
        // Arrange — kid is the first 16 chars of the Base64 SHA-256 of the public key
        using var rsa = RSA.Create();
        rsa.ImportFromPem(TestJwtKeys.PublicPem);
        var expectedKid = Convert.ToBase64String(SHA256.HashData(rsa.ExportRSAPublicKey()))[..16];

        // Act
        var response = await Client.GetAsync("/.well-known/jwks.json");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var key = (await ReadJsonAsync(response)).GetProperty("keys")[0];
        key.GetProperty("kty").GetString().Should().Be("RSA");
        key.GetProperty("use").GetString().Should().Be("sig");
        key.GetProperty("alg").GetString().Should().Be("PS256");
        key.GetProperty("kid").GetString().Should().Be(expectedKid);
        key.GetProperty("n").GetString().Should().NotBeNullOrEmpty("the modulus must be exposed");
        key.GetProperty("e").GetString().Should().NotBeNullOrEmpty("the exponent must be exposed");
    }
}
