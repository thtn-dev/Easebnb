using BuildingBlocks.Application.ObjectStorage.Abstractions;
using Easebnb.Identity.Infrastructure;
using Easebnb.Identity.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Testcontainers.PostgreSql;

namespace Easebnb.Identity.IntegrationTests;

public class IdentityModuleFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("easebnb_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public IServiceProvider Services { get; set; } = null!;
    public Mock<IObjectStorage> ObjectStorageMock { get; } = new();
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Database:ConnectionString", _dbContainer.GetConnectionString() }
            })
            .Build();
        services.AddLogging();

        services.AddIdentityModule(configuration);

        services.RemoveAll<IObjectStorage>();
        services.AddScoped(_ => ObjectStorageMock.Object);

        Services = services.BuildServiceProvider();

        // chỉ migrate DbContext của module Identity
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _dbContainer.DisposeAsync();
}