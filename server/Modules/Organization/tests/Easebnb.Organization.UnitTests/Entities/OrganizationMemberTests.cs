using Easebnb.Organization.Core.Entities;

namespace Easebnb.Organization.UnitTests.Entities;

public class OrganizationMemberTests
{
    [Fact]
    public void Create_WhenCalled_SetsVersion7IdAndMembershipFields()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = OrganizationMember.Create(organizationId, userId, OrganizationMemberRole.Admin);

        member.Id.Should().NotBeEmpty();
        member.Id.Version.Should().Be(7, "ids must be time-ordered GUID v7 values");
        member.OrganizationId.Should().Be(organizationId);
        member.UserId.Should().Be(userId);
        member.Role.Should().Be(OrganizationMemberRole.Admin);
    }

    [Fact]
    public void ChangeRole_WhenCalled_UpdatesRole()
    {
        var member = OrganizationMember.Create(Guid.NewGuid(), Guid.NewGuid(), OrganizationMemberRole.Member);

        member.ChangeRole(OrganizationMemberRole.Manager);

        member.Role.Should().Be(OrganizationMemberRole.Manager);
    }
}
