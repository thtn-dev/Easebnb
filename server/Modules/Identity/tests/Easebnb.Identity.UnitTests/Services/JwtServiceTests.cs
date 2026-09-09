using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Services;
using Easebnb.Identity.Infrastructure.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Easebnb.Identity.UnitTests.Services;

public class JwtServiceTests : IDisposable
{
    private readonly RSA _privateRsa;
    private readonly RSA _publicRsa;
    private readonly JwtSettings _settings;
    private readonly JwtService _sut;

    public JwtServiceTests()
    {
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        _privateRsa = RSA.Create(2048);
        _publicRsa = RSA.Create();

        _publicRsa.ImportParameters(_privateRsa.ExportParameters(false));

        _settings = new JwtSettings
        {
            Issuer = "Easebnb",
            Audience = "Easebnb.Api",
            AccessTokenExpiryMinutes = 60
        };

        var keyProvider = new TestRsaKeyProvider(_privateRsa, _publicRsa);

        _sut = new JwtService(
            keyProvider,
            NullLogger<JwtService>.Instance,
            Options.Create(_settings));
    }

    [Fact]
    public void GenerateToken_ShouldReturnValidJwt()
    {
        // Act
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        // Assert
        token.Should().NotBeNullOrWhiteSpace();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("Easebnb");
        jwt.Audiences.Should().Contain("Easebnb.Api");
        jwt.Header.Alg.Should().Be(SecurityAlgorithms.RsaSsaPssSha256);
    }

    [Fact]
    public void GenerateToken_ShouldContainUserId()
    {
        // Act
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        "user-123".Should().Be(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Fact]
    public void GenerateToken_ShouldContainUsername()
    {
        // Act
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        "john.doe".Should().Be(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value);
    }

    [Fact]
    public void GenerateToken_ShouldContainJti()
    {
        // Act
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        var jti = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);

        jti.Should().NotBeNull();
        Guid.TryParse(jti.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateToken_ShouldContainRoles()
    {
        // Arrange
        var roles = new List<string>
        {
            "Admin",
            "Host"
        };

        // Act
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            roles);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        var tokenRoles = jwt.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        tokenRoles.Should().HaveCount(2);
        tokenRoles.Should().Contain("Admin");
        tokenRoles.Should().Contain("Host");
    }

    [Fact]
    public void GenerateToken_ShouldContainCustomClaims()
    {
        // Arrange
        var customClaims = new List<Claim>
        {
            new("email", "john@example.com"),
            new("tenant_id", "tenant-123")
        };

        // Act
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            [],
            customClaims);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        jwt.Claims.First(c => c.Type == "email").Value
            .Should().Be("john@example.com");

        jwt.Claims.First(c => c.Type == "tenant_id").Value
            .Should().Be("tenant-123");
    }

    [Fact]
    public void GenerateToken_ShouldSetExpiration()
    {
        // Act
        var before = DateTime.UtcNow.AddMinutes(59);
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);
        var after = DateTime.UtcNow.AddMinutes(61);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        jwt.ValidTo.Should().BeAfter(before);
        jwt.ValidTo.Should().BeBefore(after);
    }

    [Fact]
    public void GenerateToken_ShouldGenerateDifferentJtiForEachToken()
    {
        // Act
        var token1 = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        var token2 = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        var jwt1 = new JwtSecurityTokenHandler().ReadJwtToken(token1);
        var jwt2 = new JwtSecurityTokenHandler().ReadJwtToken(token2);

        // Assert
        var jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateToken_ShouldBeValidWithPublicKey()
    {
        // Arrange
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,

            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = new RsaSecurityKey(_publicRsa),

            ValidAlgorithms =
            [
                SecurityAlgorithms.RsaSsaPssSha256
            ]
        };

        // Act
        var principal = new JwtSecurityTokenHandler()
            .ValidateToken(
                token,
                validationParameters,
                out var validatedToken);

        // Assert
        principal.Should().NotBeNull();
        validatedToken.Should().NotBeNull();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyToken()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldGenerateDifferentTokens()
    {
        // Act
        var token1 = _sut.GenerateRefreshToken();
        var token2 = _sut.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldHaveExpectedLength()
    {
        // Act
        var token = _sut.GenerateRefreshToken();

        // 64 random bytes -> Base64 string = 88 characters
        token.Length.Should().Be(88);
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldReturnPrincipal()
    {
        // Arrange
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "user-123"),
            new(JwtRegisteredClaimNames.UniqueName, "john.doe"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, "Admin")
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(_privateRsa),
            SecurityAlgorithms.RsaSsaPssSha256);

        var token = new JwtSecurityToken(
            _settings.Issuer,
            _settings.Audience,
            claims,
            now.AddMinutes(-20),
            now.AddMinutes(-10),
            credentials);

        var tokenString = new JwtSecurityTokenHandler()
            .WriteToken(token);

        // Act
        var principal = _sut.GetPrincipalFromExpiredToken(tokenString);

        // Assert
        principal.Should().NotBeNull();

        principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            .Should().Be("user-123");

        principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            .Should().Be("john.doe");

        principal.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldRejectInvalidSignature()
    {
        // Arrange
        var token = _sut.GenerateToken(
            "user-123",
            "john.doe",
            []);

        using var anotherRsa = RSA.Create(2048);

        var invalidToken = CreateTokenWithKey(
            "user-123",
            "john.doe",
            _settings,
            anotherRsa);

        // Act
        var principal = _sut.GetPrincipalFromExpiredToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldRejectInvalidIssuer()
    {
        // Arrange
        var invalidSettings = new JwtSettings
        {
            Issuer = "AnotherIssuer",
            Audience = _settings.Audience,
            AccessTokenExpiryMinutes = 60
        };

        var invalidToken = CreateTokenWithSettings(
            "user-123",
            "john.doe",
            invalidSettings,
            _privateRsa);

        // Act
        var principal = _sut.GetPrincipalFromExpiredToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldRejectInvalidAudience()
    {
        // Arrange
        var invalidSettings = new JwtSettings
        {
            Issuer = _settings.Issuer,
            Audience = "AnotherAudience",
            AccessTokenExpiryMinutes = 60
        };

        var invalidToken = CreateTokenWithSettings(
            "user-123",
            "john.doe",
            invalidSettings,
            _privateRsa);

        // Act
        var principal = _sut.GetPrincipalFromExpiredToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldRejectRs256Token()
    {
        // Arrange
        var token = CreateRs256Token(
            "user-123",
            "john.doe");

        // Act
        var principal = _sut.GetPrincipalFromExpiredToken(token);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void GetPrincipalFromExpiredToken_ShouldReturnNullForMalformedToken()
    {
        // Act
        var principal = _sut.GetPrincipalFromExpiredToken(
            "this-is-not-a-jwt");

        // Assert
        principal.Should().BeNull();
    }

    private string CreateTokenWithSettings(
        string userId,
        string username,
        JwtSettings settings,
        RSA signingRsa)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(signingRsa),
            SecurityAlgorithms.RsaSsaPssSha256);

        var token = new JwtSecurityToken(
            settings.Issuer,
            settings.Audience,
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(settings.AccessTokenExpiryMinutes),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string CreateTokenWithKey(
        string userId,
        string username,
        JwtSettings settings,
        RSA signingRsa)
    {
        return CreateTokenWithSettings(
            userId,
            username,
            settings,
            signingRsa);
    }

    private string CreateRs256Token(
        string userId,
        string username)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString())
        };

        var credentials = new SigningCredentials(
            new RsaSecurityKey(_privateRsa),
            SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            _settings.Issuer,
            _settings.Audience,
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(60),
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        _privateRsa.Dispose();
        _publicRsa.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TestRsaKeyProvider(
        RSA privateRsa,
        RSA publicRsa) : IRsaKeyProvider
    {
        public RsaSecurityKey PrivateKey { get; } = new(privateRsa);

        public RsaSecurityKey PublicKey { get; } = new(publicRsa);

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}