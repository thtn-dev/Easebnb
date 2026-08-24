using ErrorOr;

namespace Easebnb.Organization.UnitTests.Services;

using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Infrastructure.Services;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

public class OrganizationAccessTests
{
    private static Organization CreateActiveOrganization() =>
        Organization.Create("My Hotel", "my-hotel", null, Guid.NewGuid());

    // ---------------------------------------------------------------
    // EnsureActive
    // ---------------------------------------------------------------

    [Fact]
    public void EnsureActive_WhenOrganizationIsActive_ReturnsNoError()
    {
        OrganizationAccess.EnsureActive(CreateActiveOrganization()).Should().BeNull();
    }

    [Fact]
    public void EnsureActive_WhenOrganizationIsNotActive_ReturnsConflict()
    {
        var organization = CreateActiveOrganization();
        organization.Archive();

        var error = OrganizationAccess.EnsureActive(organization);

        error.Should().NotBeNull();
        error!.Value.Type.Should().Be(ErrorType.Conflict);
        error.Value.Description.Should().Be("Organization is not active");
    }

    // ---------------------------------------------------------------
    // EnsureMember
    // ---------------------------------------------------------------

    [Fact]
    public void EnsureMember_WhenMembershipIsMissing_ReturnsForbidden()
    {
        var error = OrganizationAccess.EnsureMember(null);

        error.Should().NotBeNull();
        error!.Value.Type.Should().Be(ErrorType.Forbidden);
        error.Value.Description.Should().Be("You are not a member of this organization");
    }

    [Fact]
    public void EnsureMember_WhenMembershipExists_ReturnsNoError()
    {
        var membership = OrganizationMember.Create(Guid.NewGuid(), Guid.NewGuid(), OrganizationMemberRole.Member);

        OrganizationAccess.EnsureMember(membership).Should().BeNull();
    }

    // ---------------------------------------------------------------
    // EnsureAdministrator / EnsureOwner
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(OrganizationMemberRole.Owner)]
    [InlineData(OrganizationMemberRole.Admin)]
    public void EnsureAdministrator_WhenRoleIsOwnerOrAdmin_ReturnsNoError(OrganizationMemberRole role)
    {
        var membership = OrganizationMember.Create(Guid.NewGuid(), Guid.NewGuid(), role);

        OrganizationAccess.EnsureAdministrator(membership).Should().BeNull();
    }

    [Theory]
    [InlineData(OrganizationMemberRole.Manager)]
    [InlineData(OrganizationMemberRole.Member)]
    public void EnsureAdministrator_WhenRoleIsManagerOrMember_ReturnsForbidden(OrganizationMemberRole role)
    {
        var membership = OrganizationMember.Create(Guid.NewGuid(), Guid.NewGuid(), role);

        var error = OrganizationAccess.EnsureAdministrator(membership);

        error.Should().NotBeNull();
        error!.Value.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void EnsureOwner_WhenRoleIsOwner_ReturnsNoError()
    {
        var membership = OrganizationMember.Create(Guid.NewGuid(), Guid.NewGuid(), OrganizationMemberRole.Owner);

        OrganizationAccess.EnsureOwner(membership).Should().BeNull();
    }

    [Fact]
    public void EnsureOwner_WhenRoleIsAdmin_ReturnsForbidden()
    {
        var membership = OrganizationMember.Create(Guid.NewGuid(), Guid.NewGuid(), OrganizationMemberRole.Admin);

        var error = OrganizationAccess.EnsureOwner(membership);

        error.Should().NotBeNull();
        error!.Value.Type.Should().Be(ErrorType.Forbidden);
        error.Value.Description.Should().Be("Only the organization owner can perform this action");
    }

    // ---------------------------------------------------------------
    // IsAdministrator
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(OrganizationMemberRole.Owner, true)]
    [InlineData(OrganizationMemberRole.Admin, true)]
    [InlineData(OrganizationMemberRole.Manager, false)]
    [InlineData(OrganizationMemberRole.Member, false)]
    public void IsAdministrator_ReturnsTrueOnlyForOwnerAndAdmin(OrganizationMemberRole role, bool expected)
    {
        OrganizationAccess.IsAdministrator(role).Should().Be(expected);
    }
}
