using System.ComponentModel.DataAnnotations;

namespace Easebnb.Identity.Infrastructure.Settings;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string PrivateKey { get; set; } = string.Empty; // PEM private key
    public string PublicKey { get; set; } = string.Empty; // PEM public key
    [Required] public string Issuer { get; set; } = string.Empty;

    [Required] public string Audience { get; set; } = string.Empty;

    public int AccessTokenExpiryMinutes { get; set; } = 60;
}