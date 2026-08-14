using Microsoft.Extensions.DependencyInjection;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Services;

namespace Easebnb.Identity.Infrastructure;

public static class InfrastructureModule 
{
   public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRsaKeyProvider, RsaKeyProvider>();
        services.AddScoped<IAccountService, AccountService>();

        return services;
    }
}