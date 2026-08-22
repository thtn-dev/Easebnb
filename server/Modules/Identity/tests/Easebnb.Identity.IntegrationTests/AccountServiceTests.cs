using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;

namespace Easebnb.Identity.IntegrationTests;

public class AccountServiceTests(IdentityModuleFixture fixture) : IdentityModuleTestBase(fixture)
{
    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Succeeds()
    {
        // Arrange
        var user = new User { UserName = "test@easebnb.com", Email = "test@easebnb.com" };
        var createResult = await UserManager.CreateAsync(user, "OldPass123!");
        var request = new ChangePasswordRequest("OldPass123!", "NewPass123!", "NewPass123!");

        // Act
        var result = await AccountService.ChangePasswordAsync(user.Id, request);

        // Assert
        createResult.Succeeded.Should().BeTrue(
            string.Join(", ", createResult.Errors.Select(x => x.Description)));

        (await UserManager.CheckPasswordAsync(user, "OldPass123!"))
            .Should().BeFalse();

        (await UserManager.CheckPasswordAsync(user, "NewPass123!"))
            .Should().BeTrue();
    }
}