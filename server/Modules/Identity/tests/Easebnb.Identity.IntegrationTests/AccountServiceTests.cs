using System.Net;
using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Easebnb.Identity.IntegrationTests;

public class AccountServiceTests(IdentityApiFixture fixture) : IdentityApiTestBase(fixture)
{
    private IAccountService CreateAccountService()
    {
        var scope = CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAccountService>();
    }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Succeeds()
    {
        // Arrange
        var user = await RegisterUserAsync();
        var persisted = await GetUserByUsernameAsync(user.Username);
        var accountService = CreateAccountService();
        var request = new ChangePasswordRequest(user.Password, "NewPass123!", "NewPass123!");

        // Act
        var result = await accountService.ChangePasswordAsync(persisted.Id, request);

        // Assert
        result.IsError.Should().BeFalse();
        var oldPasswordLogin = await PostJsonAsync(LoginUrl, new { username = user.Username, password = user.Password });
        oldPasswordLogin.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the old password must stop working after a change");

        var newPasswordLogin = await PostJsonAsync(LoginUrl, new { username = user.Username, password = "NewPass123!" });
        newPasswordLogin.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
