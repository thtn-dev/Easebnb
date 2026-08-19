using BuildingBlocks.Infrastructure;
using BuildingBlocks.SharedKernel;
using Easebnb.Database;
using Easebnb.Identity.Core.Entities;
using Microsoft.Extensions.DependencyInjection;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.Identity.Infrastructure.Services;
using Easebnb.Identity.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Easebnb.Identity.Infrastructure;

public static class InfrastructureModule 
{
   extension(IServiceCollection services)
   {
       public IServiceCollection AddIdentityModule(IConfiguration configuration)
       {
           var databaseSection = configuration.GetSection(DatabaseSettings.SectionName);
           services.AddOptions<DatabaseSettings>()
               .Bind(databaseSection)
               .ValidateDataAnnotations()
               .ValidateOnStart();
           services.AddScoped<IUnitOfWork, UnitOfWork<AppIdentityDbContext>>();
           services.AddAspNetIdentityServices(configuration);
           services.AddDatabase<AppIdentityDbContext>("Identity");
           services.AddScoped<IJwtService, JwtService>();
           services.AddScoped<IAuthService, AuthService>();
           services.AddScoped<IRsaKeyProvider, RsaKeyProvider>();
           services.AddScoped<IAccountService, AccountService>();
        
           return services;
       }

       private void AddAspNetIdentityServices(IConfiguration configuration)
       {
           var jwtSettingsSection = configuration.GetSection(JwtSettings.SectionName);
           services.AddOptions<JwtSettings>()
               .Bind(jwtSettingsSection)
               .ValidateDataAnnotations()
               .ValidateOnStart();

           services.AddIdentity<User, Role>(options =>
               {
                   options.Password.RequireDigit = true;
                   options.Password.RequireLowercase = true;
                   options.Password.RequireUppercase = true;
                   options.Password.RequireNonAlphanumeric = false;
                   options.Password.RequiredLength = 6;

                   options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                   options.Lockout.MaxFailedAccessAttempts = 5;
                   options.Lockout.AllowedForNewUsers = true;

                   options.User.RequireUniqueEmail = true;
                   options.SignIn.RequireConfirmedEmail = false;
               })
               .AddEntityFrameworkStores<AppIdentityDbContext>()
               .AddDefaultTokenProviders();

           services.AddAuthentication(options =>
               {
                   options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                   options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                   options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
               })
               .AddJwtBearer();
           services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

           services.AddAuthorization();
       }
   }
}


public sealed class ConfigureJwtBearerOptions(
    IOptions<JwtSettings> jwtSettings,
    IRsaKeyProvider keyProvider)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options)
    {
        Configure(JwtBearerDefaults.AuthenticationScheme, options);
    }

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme) return;

        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings.Value.Issuer,
            ValidAudience = jwtSettings.Value.Audience,

            IssuerSigningKey = keyProvider.PublicKey,

            ValidAlgorithms = [SecurityAlgorithms.RsaSsaPssSha256],

            ClockSkew = TimeSpan.FromMinutes(1)
        };
    }
}