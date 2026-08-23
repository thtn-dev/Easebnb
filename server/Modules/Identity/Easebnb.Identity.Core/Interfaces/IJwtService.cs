using System.Security.Claims;

namespace Easebnb.Identity.Core.Interfaces;

public interface IJwtService
{
    string GenerateToken(string userId, string username, List<string> roles, List<Claim>? customClaims = null);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}