using BuildingBlocks.Infrastructure;
using BuildingBlocks.SharedKernel;
using Easebnb.Database;
using Easebnb.Organization.Core.Interfaces;
using Easebnb.Organization.Infrastructure.Database;
using Easebnb.Organization.Infrastructure.Services;
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

            // The un-keyed IUnitOfWork slot is owned by the Identity module;
            // this module's services resolve their unit of work by key.
            services.AddKeyedScoped<IUnitOfWork, UnitOfWork<OrganizationDbContext>>("Organization");

            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IOrganizationMemberService, OrganizationMemberService>();

            return services;
        }
    }
}
