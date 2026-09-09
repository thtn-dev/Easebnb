using System.Security.Cryptography;

namespace Easebnb.Identity.IntegrationTests;

/// <summary>
///     RSA key pair generated once per test run and injected into the API host
///     configuration, replacing the PEM keys normally loaded from WebApi's .env.
/// </summary>
internal static class TestJwtKeys
{
    public const string Issuer = "easebnb-test";
    public const string Audience = "easebnb-test";

    public static string PrivatePem { get; }

    public static string PublicPem { get; }

    static TestJwtKeys()
    {
        using var rsa = RSA.Create(2048);
        PrivatePem = rsa.ExportPkcs8PrivateKeyPem();
        PublicPem = rsa.ExportSubjectPublicKeyInfoPem();
    }
}
