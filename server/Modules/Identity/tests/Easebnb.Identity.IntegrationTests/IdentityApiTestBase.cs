using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Application;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;
using Easebnb.Identity.Infrastructure.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Identity.IntegrationTests;

[Collection("IdentityApi")]
public abstract class IdentityApiTestBase(IdentityApiFixture fixture) : IAsyncLifetime
{
    protected const string RegisterUrl = "/api/v1/auth/register";
    protected const string LoginUrl = "/api/v1/auth/login";

    protected readonly IdentityApiFixture Fixture = fixture;
    protected readonly HttpClient Client = fixture.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Client.Dispose();

        // Reset shared state so tests are isolated from each other. The roles
        // table is kept: the "user" role is static seed data needed by every
        // registration. lock_timeout guards against a leaked transaction from
        // a failing test blocking cleanup for the full command timeout.
        await using var scope = Fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            SET lock_timeout = '5s';
            TRUNCATE TABLE identity.users, identity.refresh_tokens,
                         identity.user_roles, identity.user_claims,
                         identity.user_logins, identity.user_tokens, identity.role_claims
            """);
        Fixture.ObjectStorageMock.Invocations.Clear();
        Fixture.EmailHandler.Reset();
    }

    protected IServiceScope CreateScope() => Fixture.Services.CreateScope();

    protected Task<HttpResponseMessage> PostJsonAsync(string requestUri, object value) =>
        Client.PostAsJsonAsync(requestUri, value);

    protected Task<HttpResponseMessage> PutJsonAsync(string requestUri, object value) =>
        Client.PutAsJsonAsync(requestUri, value);

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    protected sealed record TestUser(string Username, string Email, string Password);

    protected static TestUser NewUserCredentials()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        return new TestUser($"user{suffix}", $"user{suffix}@test.com", "Pass123!");
    }

    /// <summary>
    ///     Registers a user through the API. Credentials are unique per call when omitted.
    /// </summary>
    protected async Task<TestUser> RegisterUserAsync(TestUser? credentials = null)
    {
        var user = credentials ?? NewUserCredentials();
        var response = await PostJsonAsync(RegisterUrl, new
        {
            username = user.Username,
            email = user.Email,
            password = user.Password,
            confirmPassword = user.Password
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            $"user seeding failed: {await response.Content.ReadAsStringAsync()}");
        return user;
    }

    protected async Task<LoginResponse> LoginAsync(string username, string password)
    {
        var response = await PostJsonAsync(LoginUrl, new { username, password });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"login failed: {await response.Content.ReadAsStringAsync()}");
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
        envelope?.Should().NotBeNull();
        envelope!.Success.Should().BeTrue();
        return envelope.Data!;
    }

    /// <summary>
    ///     Registers a fresh user, logs in, and returns an HttpClient with the Bearer token set.
    /// </summary>
    protected async Task<(HttpClient Client, LoginResponse Login)> CreateAuthorizedClientAsync()
    {
        var user = await RegisterUserAsync();
        return await LoginAsAsync(user);
    }

    /// <summary>
    ///     Logs in an already-registered user and returns an HttpClient with the Bearer token set.
    /// </summary>
    protected async Task<(HttpClient Client, LoginResponse Login)> LoginAsAsync(TestUser user)
    {
        var login = await LoginAsync(user.Username, user.Password);
        var client = Fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return (client, login);
    }

    protected async Task<User> GetUserByUsernameAsync(string username)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        return await db.Users.AsNoTracking().SingleAsync(u => u.UserName == username);
    }

    protected async Task<string> GenerateEmailConfirmationTokenAsync(Guid userId)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return EncodeToken(await userManager.GenerateEmailConfirmationTokenAsync(user!));
    }

    protected async Task<string> GeneratePasswordResetTokenAsync(string email)
    {
        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByEmailAsync(email);
        return EncodeToken(await userManager.GeneratePasswordResetTokenAsync(user!));
    }

    /// <summary>
    ///     Identity tokens are transported Base64Url-encoded (the endpoints decode them again).
    /// </summary>
    protected static string EncodeToken(string rawToken) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
}

[CollectionDefinition("IdentityApi")]
public class IdentityApiCollection : ICollectionFixture<IdentityApiFixture>;
