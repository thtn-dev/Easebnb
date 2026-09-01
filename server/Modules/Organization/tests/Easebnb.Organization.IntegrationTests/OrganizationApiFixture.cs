using BuildingBlocks.Application.ObjectStorage.Abstractions;
using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.Identity.Infrastructure.Services;
using Easebnb.Organization.Infrastructure.Database;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Easebnb.Organization.IntegrationTests;

/// <summary>
///     Hosts the real WebApi (Program.cs) on an in-memory test server backed by a
///     PostgreSQL Testcontainer. Configuration is overridden in memory: the
///     database connection string and a per-run RSA key pair replace the values
///     normally provided by appsettings/.env (DotNetEnv finds no .env file when
///     running from the test host). Object storage and the email handler are
///     replaced with test doubles.
/// </summary>
public sealed class OrganizationApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18-alpine3.23")
        .WithDatabase("easebnb_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public Mock<IObjectStorage> ObjectStorageMock { get; } = new();

    public FakeSendEmailHandler EmailHandler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = _dbContainer.GetConnectionString(),
                // Integration events flow over the in-memory transport in tests;
                // never connect to a broker from the test host.
                ["RabbitMq:Enabled"] = "false",
                ["Jwt:Issuer"] = TestJwtKeys.Issuer,
                ["Jwt:Audience"] = TestJwtKeys.Audience,
                ["Jwt:PrivateKey"] = TestJwtKeys.PrivatePem,
                ["Jwt:PublicKey"] = TestJwtKeys.PublicPem
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IObjectStorage>();
            services.AddScoped(_ => ObjectStorageMock.Object);

            services.RemoveAll<INotificationHandler<SendEmailEvent>>();
            services.AddSingleton<INotificationHandler<SendEmailEvent>>(EmailHandler);
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Trigger host build (applies the configuration above), then migrate.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.Database.MigrateAsync();

        // The organization schema holds the module's business tables plus the
        // outbox/inbox used by the UserRegisteredIntegrationEvent consumer.
        var organizationDb = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await organizationDb.Database.MigrateAsync();

        // RegisterAsync assigns the "user" role, so it must exist before any
        // registration. Migrations only create the schema, not seed data.
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        if (!await roleManager.RoleExistsAsync("user"))
            await roleManager.CreateAsync(new Role { Name = "user" });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }
}
