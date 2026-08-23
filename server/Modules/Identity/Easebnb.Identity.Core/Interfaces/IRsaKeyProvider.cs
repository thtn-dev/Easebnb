using Microsoft.IdentityModel.Tokens;

namespace Easebnb.Identity.Core.Interfaces;

public interface IRsaKeyProvider
{
    RsaSecurityKey PrivateKey { get; }
    RsaSecurityKey PublicKey { get; }

    ValueTask DisposeAsync();
}