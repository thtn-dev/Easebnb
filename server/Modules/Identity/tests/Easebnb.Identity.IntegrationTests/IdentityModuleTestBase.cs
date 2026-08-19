using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Core.Interfaces;
using Easebnb.Identity.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Identity.IntegrationTests;

[Collection("IdentityModule")]
public abstract class IdentityModuleTestBase : IAsyncLifetime
{
    protected readonly IdentityModuleFixture Fixture;
    protected readonly IServiceScope Scope;
    protected readonly AppIdentityDbContext DbContext;
    protected readonly UserManager<User> UserManager;
    protected readonly IAccountService AccountService;

    protected IdentityModuleTestBase(IdentityModuleFixture fixture)
    {
        Fixture = fixture;
        Scope = fixture.Services.CreateScope();
        DbContext = Scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        UserManager = Scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        AccountService = Scope.ServiceProvider.GetRequiredService<IAccountService>();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        DbContext.Users.RemoveRange(DbContext.Users);
        await DbContext.SaveChangesAsync();
        Scope.Dispose();
    }
}

[CollectionDefinition("IdentityModule")]
public class IdentityModuleCollection : ICollectionFixture<IdentityModuleFixture> { }