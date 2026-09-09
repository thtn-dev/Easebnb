using System.Net;

namespace Easebnb.Identity.IntegrationTests.Auth;

public class RegisterEndpointTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    [Fact]
    public async Task Register_WithValidBody_Returns204AndPersistsUnconfirmedUser()
    {
        // Arrange
        var user = NewUserCredentials();

        // Act
        var response = await PostJsonAsync(RegisterUrl, new
        {
            username = user.Username,
            email = user.Email,
            password = user.Password,
            confirmPassword = user.Password
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var persisted = await GetUserByUsernameAsync(user.Username);
        persisted.Email.Should().Be(user.Email);
        persisted.EmailConfirmed.Should().BeFalse("registration must not auto-confirm the email");

        var emailEvent = Fixture.EmailHandler.Events.Should().ContainSingle().Subject;
        emailEvent.Email.Should().Be(user.Email);
        emailEvent.Subject.Should().Be("Confirm your email");
        emailEvent.Body.Should().Be("Please confirm your email by clicking the link.");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Returns409()
    {
        // Arrange
        var user = await RegisterUserAsync();

        // Act
        var response = await PostJsonAsync(RegisterUrl, new
        {
            username = user.Username,
            email = "different@test.com",
            password = user.Password,
            confirmPassword = user.Password
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409()
    {
        // Arrange
        var user = await RegisterUserAsync();

        // Act
        var response = await PostJsonAsync(RegisterUrl, new
        {
            username = "differentuser",
            email = user.Email,
            password = user.Password,
            confirmPassword = user.Password
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithPasswordMismatch_Returns400ValidationProblem()
    {
        // Arrange
        var user = NewUserCredentials();

        // Act
        var response = await PostJsonAsync(RegisterUrl, new
        {
            username = user.Username,
            email = user.Email,
            password = user.Password,
            confirmPassword = "Different123!"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        body.GetProperty("errors").EnumerateObject().Should().Contain(p => p.Name == "ConfirmPassword",
            "the mismatch is rejected by the request validator");
    }

    [Fact]
    public async Task Register_WithInvalidBody_Returns400ValidationProblem()
    {
        // Act
        var response = await PostJsonAsync(RegisterUrl, new
        {
            username = "user",
            email = "not-an-email",
            password = "123",
            confirmPassword = "123"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await ReadJsonAsync(response);
        var errors = body.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        errors.Should().Contain("Email");
        errors.Should().Contain("Password");
    }
}
