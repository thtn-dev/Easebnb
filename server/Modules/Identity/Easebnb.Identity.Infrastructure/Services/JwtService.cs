using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Settings;

namespace Easebnb.Identity.Infrastructure.Services;

public class JwtService(IRsaKeyProvider keyProvider, ILogger<JwtService> logger, IOptions<JwtSettings> options)
    : IJwtService
{
    private readonly JwtSettings _settings = options.Value;

    public string GenerateToken(
        string userId,
        string username,
        List<string> roles,
        List<Claim>? customClaims = null)
    {
        logger.LogInformation("Generating JWT for user {UserId} with roles: {Roles}",
            userId, string.Join(", ", roles));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (customClaims is { Count: > 0 })
            claims.AddRange(customClaims);

        // PS256 = RSA-PSS + SHA256
        var credentials = new SigningCredentials(
            keyProvider.PrivateKey,
            SecurityAlgorithms.RsaSsaPssSha256
        );

        var token = new JwtSecurityToken(
            _settings.Issuer,
            _settings.Audience,
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = keyProvider.PublicKey, // use public key to validate signature
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.RsaSsaPssSha256] // block tokens signed with other algorithms
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(token, validationParams, out var securityToken);

            if (securityToken is not JwtSecurityToken jwt ||
                !jwt.Header.Alg.Equals(
                    SecurityAlgorithms.RsaSsaPssSha256,
                    StringComparison.OrdinalIgnoreCase))
                return null;

            return principal;
        }
        catch
        {
            return null;
        }
    }
}