using Easebnb.Identity.Core.Dtos;
using Easebnb.Identity.Core.Entities;
using FluentAssertions;

namespace Easebnb.Identity.IntegrationTests;

public class AccountServiceTests : IdentityModuleTestBase
{
    public AccountServiceTests(IdentityModuleFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Succeeds()
    {
        var user = new User { UserName = "test@easebnb.com", Email = "test@easebnb.com" };
        await UserManager.CreateAsync(user, "OldPass123!");

        var request = new ChangePasswordRequest("OldPass123!", "NewPass123!", "NewPass123!");

        var result = await AccountService.ChangePasswordAsync(user.Id, request);

        result.IsError.Should().BeFalse();
    }
}