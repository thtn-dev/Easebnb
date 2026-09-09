using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BuildingBlocks.Application;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Infrastructure.Database;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Easebnb.Organization.IntegrationTests;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

[Collection("OrganizationApi")]
public abstract class OrganizationApiTestBase(OrganizationApiFixture fixture) : IAsyncLifetime
{
    protected const string OrganizationsUrl = "/api/v1/organizations";
    protected const string RegisterUrl = "/api/v1/auth/register";
    protected const string LoginUrl = "/api/v1/auth/login";

    protected readonly OrganizationApiFixture Fixture = fixture;
    protected readonly HttpClient Client = fixture.CreateClient();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Client.Dispose();

        // Reset shared state so tests are isolated from each other. The roles
        // table is kept: the "user" role is static seed data needed by every
        // registration. The outbox/inbox tables are truncated as well so a
        // pending message from a failing test cannot leak into the next one.
        // lock_timeout guards against a leaked transaction from a failing test
        // blocking cleanup for the full command timeout.
        await using var scope = Fixture.Services.CreateAsyncScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<AppIdentityDbContext>();
        await identityDb.Database.ExecuteSqlRawAsync("""
            SET lock_timeout = '5s';
            TRUNCATE TABLE identity.users, identity.refresh_tokens,
                         identity.user_roles, identity.user_claims,
                         identity.user_logins, identity.user_tokens, identity.role_claims;
            TRUNCATE TABLE organization.organizations, organization.organization_members,
                         organization.registered_users, organization.outbox_state,
                         organization.outbox_message, organization.inbox_state
            """);
        Fixture.ObjectStorageMock.Invocations.Clear();
        Fixture.EmailHandler.Reset();
    }

    // ---------------------------------------------------------------
    // HTTP + JSON helpers
    // ---------------------------------------------------------------

    protected Task<HttpResponseMessage> PostJsonAsync(string requestUri, object value) =>
        Client.PostAsJsonAsync(requestUri, value);

    protected Task<HttpResponseMessage> PutJsonAsync(string requestUri, object value) =>
        Client.PutAsJsonAsync(requestUri, value);

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // ---------------------------------------------------------------
    // Identity helpers (register / login / authorized clients)
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // Organization helpers
    // ---------------------------------------------------------------

    /// <summary>
    ///     Creates an organization through the API as the given client and
    ///     returns the created organization.
    /// </summary>
    protected async Task<OrganizationResponse> CreateOrganizationAsync(
        HttpClient client,
        string name = "My Hotel",
        string? slug = null,
        string? description = null)
    {
        var response = await client.PostAsJsonAsync(OrganizationsUrl, new { name, slug, description });
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"organization seeding failed: {await response.Content.ReadAsStringAsync()}");
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse<OrganizationResponse>>();
        envelope!.Success.Should().BeTrue();
        return envelope.Data!;
    }

    /// <summary>
    ///     Seeds a membership row directly in the organization schema (faster
    ///     than driving the whole add-member flow through the API).
    /// </summary>
    protected async Task SeedMembershipAsync(Guid organizationId, Guid userId, OrganizationMemberRole role)
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        db.OrganizationMembers.Add(OrganizationMember.Create(organizationId, userId, role));
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Seeds a registered-user projection row directly. Used by tests that
    ///     verify member-management behavior; the real projection pipeline is
    ///     covered by <c>UserRegisteredProjectionTests</c>.
    /// </summary>
    protected async Task SeedRegisteredUserAsync(Guid userId, string? email = null, string? userName = null)
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        db.RegisteredUsers.Add(RegisteredUser.Create(
            userId, email ?? $"{userId:N}@test.com", userName));
        await db.SaveChangesAsync();
    }

    protected async Task<Organization> GetOrganizationFromDbAsync(Guid organizationId)
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        return await db.Organizations.AsNoTracking().SingleAsync(o => o.Id == organizationId);
    }

    protected async Task<OrganizationMember?> GetMembershipFromDbAsync(Guid organizationId, Guid userId)
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        return await db.OrganizationMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
    }

    /// <summary>
    ///     Waits until the user-registered integration event has been consumed
    ///     and the user appears in the registered_users projection. The outbox
    ///     delivery service polls every ~2s, so allow a generous timeout.
    /// </summary>
    protected async Task<RegisteredUser> WaitForRegisteredUserAsync(
        Guid userId,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = Fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
            var registeredUser = await db.RegisteredUsers.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (registeredUser is not null) return registeredUser;

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"User {userId} did not appear in organization.registered_users within {timeout ?? TimeSpan.FromSeconds(15)}. " +
            "The integration-event pipeline (outbox -> in-memory transport -> consumer) did not deliver.");
    }

    // ---------------------------------------------------------------
    // Logo upload helpers
    // ---------------------------------------------------------------

    /// <summary>
    ///     Configures the object-storage mock to accept uploads and echo the
    ///     requested bucket/key back in the result.
    /// </summary>
    protected void SetupSuccessfulLogoUpload()
    {
        Fixture.ObjectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Returns((PutObjectRequest request, CancellationToken _) =>
                Task.FromResult(new PutObjectResult { Bucket = request.Bucket, Key = request.Key, ETag = "etag" }));
    }

    /// <summary>A tiny valid JPEG that survives validation and the ImageSharp re-encode.</summary>
    protected static byte[] GenerateJpegBytes()
    {
        using var image = new Image<Rgba32>(1, 1);
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }

    protected static MultipartFormDataContent CreateLogoContent(byte[] bytes, string fileName = "logo.jpg")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(file, "file", fileName);
        return content;
    }
}

[CollectionDefinition("OrganizationApi")]
public class OrganizationApiCollection : ICollectionFixture<OrganizationApiFixture>;
