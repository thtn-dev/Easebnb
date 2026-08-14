using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Settings;

namespace Easebnb.Identity.Infrastructure.Services;

public class RsaKeyProvider : IRsaKeyProvider, IAsyncDisposable
{
    private readonly RSA _privateRsa;
    private readonly RSA _publicRsa;

    public RsaKeyProvider(IOptions<JwtSettings> options)
    {
        var settings = options.Value;

        _privateRsa = RSA.Create();
        _privateRsa.ImportFromPem(settings.PrivateKey);

        _publicRsa = RSA.Create();
        _publicRsa.ImportFromPem(settings.PublicKey);

        var keyId = ComputeKeyId(_publicRsa);

        PrivateKey = new RsaSecurityKey(_privateRsa) { KeyId = keyId };
        PublicKey = new RsaSecurityKey(_publicRsa) { KeyId = keyId };
    }

    public RsaSecurityKey PrivateKey { get; }
    public RsaSecurityKey PublicKey { get; }


    public async ValueTask DisposeAsync()
    {
        await CastAndDispose(_privateRsa);
        await CastAndDispose(_publicRsa);

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var pubBytes = rsa.ExportRSAPublicKey();
        var hash = SHA256.HashData(pubBytes);
        return Convert.ToBase64String(hash)[..16];
    }
}