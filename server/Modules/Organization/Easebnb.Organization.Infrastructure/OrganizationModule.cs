using Easebnb.Database;
using Easebnb.Organization.Infrastructure.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Easebnb.Organization.Infrastructure;

public static class OrganizationModule
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddOrganizationModule(IConfiguration configuration)
        {
            services.AddOptions<DatabaseSettings>()
                .Bind(configuration.GetSection(DatabaseSettings.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddDatabase<OrganizationDbContext>("Organization");

            return services;
        }
    }
}
