namespace Easebnb.Identity.Core.Dtos;

public class GoogleTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string IdToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
}

public class GoogleUserInfo
{
    public string Sub { get; set; } = string.Empty; // Google ID
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Picture { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
}

public class GoogleLoginRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}