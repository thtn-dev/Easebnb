using Easebnb.Organization.Core.Entities;

namespace Easebnb.Organization.UnitTests.Entities;

public class RegisteredUserTests
{
    [Fact]
    public void Create_WhenCalled_UsesUserIdAsPrimaryKeyAndSetsFields()
    {
        var userId = Guid.NewGuid();

        var registeredUser = RegisteredUser.Create(userId, "user@test.com", "user");

        registeredUser.Id.Should().Be(userId);
        registeredUser.Email.Should().Be("user@test.com");
        registeredUser.UserName.Should().Be("user");
    }

    [Fact]
    public void Update_WhenCalled_ReplacesEmailAndUserName()
    {
        var registeredUser = RegisteredUser.Create(Guid.NewGuid(), "old@test.com", "old");

        registeredUser.Update("new@test.com", null);

        registeredUser.Email.Should().Be("new@test.com");
        registeredUser.UserName.Should().BeNull();
    }
}
