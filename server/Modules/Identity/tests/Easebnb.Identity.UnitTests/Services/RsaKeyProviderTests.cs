using System.Security.Cryptography;
using Easebnb.Identity.Infrastructure.Services;
using Easebnb.Identity.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Easebnb.Identity.UnitTests.Services;

public class RsaKeyProviderTests
{
    private static (string PrivatePem, string PublicPem) GeneratePemKeys()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }

    private static RsaKeyProvider CreateSut(string? privatePem = null, string? publicPem = null)
    {
        var (generatedPrivate, generatedPublic) = GeneratePemKeys();
        var settings = new JwtSettings
        {
            PrivateKey = privatePem ?? generatedPrivate,
            PublicKey = publicPem ?? generatedPublic,
            Issuer = "issuer",
            Audience = "audience"
        };
        return new RsaKeyProvider(Options.Create(settings));
    }


    // ---------------------------------------------------------------
    // Key import
    // ---------------------------------------------------------------

    [Fact]
    public async Task RsaKeyProvider_WhenGivenValidPemKeys_ExposesRsaKeys()
    {
        await using var sut = CreateSut();

        sut.PrivateKey.Rsa.Should().NotBeNull();
        sut.PrivateKey.Rsa!.KeySize.Should().Be(2048);
        sut.PublicKey.Rsa.Should().NotBeNull();
        sut.PublicKey.Rsa!.KeySize.Should().Be(2048);
    }

    [Fact]
    public async Task KeyId_WhenComputed_IsSha256PrefixOfPublicKeyOnBothKeys()
    {
        using var rsa = RSA.Create(2048);
        await using var sut = CreateSut(rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
        var expectedKeyId = Convert.ToBase64String(SHA256.HashData(rsa.ExportRSAPublicKey()))[..16];

        sut.PrivateKey.KeyId.Should().Be(expectedKeyId,
            "the kid lets JWKS consumers match tokens to the signing key");
        sut.PublicKey.KeyId.Should().Be(expectedKeyId);
        sut.PrivateKey.KeyId.Should().HaveLength(16);
    }

    [Fact]
    public void RsaKeyProvider_WhenPrivateKeyPemInvalid_ThrowsArgumentException()
    {
        var act = () => CreateSut(privatePem: "not-a-valid-pem");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RsaKeyProvider_WhenPublicKeyPemInvalid_ThrowsArgumentException()
    {
        var act = () => CreateSut(publicPem: "not-a-valid-pem");

        act.Should().Throw<ArgumentException>();
    }

    // ---------------------------------------------------------------
    // Disposal
    // ---------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_WhenCalled_CompletesWithoutError()
    {
        var sut = CreateSut();

        var act = () => sut.DisposeAsync().AsTask();

        await act.Should().NotThrowAsync();
    }
}
