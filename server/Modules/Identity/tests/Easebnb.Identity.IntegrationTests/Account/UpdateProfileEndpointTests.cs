using System.Net;
using System.Net.Http.Json;

namespace Easebnb.Identity.IntegrationTests.Account;

public class UpdateProfileEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private const string MeUrl = "/api/v1/account/me";

    [Fact]
    public async Task UpdateProfile_ChangingPhoneNumber_Returns200AndPersists()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var (client, _) = await LoginAsAsync(user);

        // Act
        var response = await client.PutAsJsonAsync(MeUrl, new { email = (string?)null, phoneNumber = "0987654321" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        data.GetProperty("phoneNumber").GetString().Should().Be("0987654321");
        (await GetUserByUsernameAsync(user.Username)).PhoneNumber.Should().Be("0987654321");
    }

    [Fact]
    public async Task UpdateProfile_ChangingEmail_Returns200AndPersists()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var (client, _) = await LoginAsAsync(user);
        var newEmail = $"renamed-{Guid.NewGuid():N}@test.com";

        // Act
        var response = await client.PutAsJsonAsync(MeUrl, new { email = newEmail, phoneNumber = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = (await ReadJsonAsync(response)).GetProperty("data");
        data.GetProperty("email").GetString().Should().Be(newEmail);
        (await GetUserByUsernameAsync(user.Username)).Email.Should().Be(newEmail);
    }

    [Fact]
    public async Task UpdateProfile_WithNoChanges_Returns400()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PutAsJsonAsync(MeUrl, new { email = (string?)null, phoneNumber = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("No changes were made");
    }

    [Fact]
    public async Task UpdateProfile_WithEmailAlreadyTaken_Returns400()
    {
        // Arrange
        var other = await RegisterUserAsync();
        var user = await RegisterUserAsync();
        var (client, _) = await LoginAsAsync(user);

        // Act
        var response = await client.PutAsJsonAsync(MeUrl, new { email = other.Email, phoneNumber = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("detail").GetString().Should().Be("Email is already taken");
    }

    [Fact]
    public async Task UpdateProfile_WithInvalidEmail_Returns400ValidationProblem()
    {
        // Arrange
        var (client, _) = await CreateAuthorizedClientAsync();

        // Act
        var response = await client.PutAsJsonAsync(MeUrl, new { email = "not-an-email", phoneNumber = (string?)null });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Select(p => p.Name).Should().Contain("Email");
    }
}
