using BuildingBlocks.Endpoints.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Easebnb.Identity.Core.Interfaces;

namespace Easebnb.WebApi.Modules.Identity.Auth;

public class JwksEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app)
    {
        app.MapGet("/.well-known/jwks.json", (IRsaKeyProvider keyProvider) =>
            {
                var rsaKey = keyProvider.PublicKey;
                var rsa = rsaKey.Rsa ?? throw new InvalidOperationException("RSA public key is not available");

                var parameters = rsa.ExportParameters(false);
                var n = Base64UrlEncoder.Encode(parameters.Modulus);
                var e = Base64UrlEncoder.Encode(parameters.Exponent);

                var jwk = new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "PS256",
                    kid = rsaKey.KeyId,
                    n,
                    e
                };

                var jwks = new { keys = new[] { jwk } };
                return Results.Json(jwks);
            })
            .WithName("jwks")
            .WithSummary("Get JSON Web Key Set (JWKS)")
            .WithDescription("Returns the JSON Web Key Set (JWKS) containing the public key")
            .WithTags("/.well-known");
    }
}