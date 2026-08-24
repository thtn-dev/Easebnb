namespace Easebnb.Organization.UnitTests.Entities;

using Easebnb.Organization.Core.Entities;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

public class OrganizationTests
{
    private static Organization CreateOrganization() =>
        Organization.Create("My Hotel", "my-hotel", "A boutique hotel", Guid.NewGuid());

    [Fact]
    public void Create_WhenCalled_SetsVersion7IdActiveStatusAndFields()
    {
        var ownerId = Guid.NewGuid();

        var organization = Organization.Create("My Hotel", "my-hotel", "A boutique hotel", ownerId);

        organization.Id.Should().NotBeEmpty();
        organization.Id.Version.Should().Be(7, "ids must be time-ordered GUID v7 values");
        organization.Name.Should().Be("My Hotel");
        organization.Slug.Should().Be("my-hotel");
        organization.Description.Should().Be("A boutique hotel");
        organization.OwnerUserId.Should().Be(ownerId);
        organization.Status.Should().Be(OrganizationStatus.Active);
        organization.IsActive.Should().BeTrue();
        organization.LogoKey.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_WhenCalled_ReplacesNameSlugAndDescription()
    {
        var organization = CreateOrganization();

        organization.UpdateDetails("New Name", "new-name", null);

        organization.Name.Should().Be("New Name");
        organization.Slug.Should().Be("new-name");
        organization.Description.Should().BeNull();
    }

    [Fact]
    public void ChangeOwner_WhenCalled_MovesOwnerReference()
    {
        var organization = CreateOrganization();
        var newOwner = Guid.NewGuid();

        organization.ChangeOwner(newOwner);

        organization.OwnerUserId.Should().Be(newOwner);
    }

    [Fact]
    public void SetLogo_WhenCalled_StoresObjectKey()
    {
        var organization = CreateOrganization();

        organization.SetLogo("organizations/1/logo/file.jpg");

        organization.LogoKey.Should().Be("organizations/1/logo/file.jpg");
    }

    [Fact]
    public void Archive_WhenCalled_MarksOrganizationArchived()
    {
        var organization = CreateOrganization();

        organization.Archive();

        organization.Status.Should().Be(OrganizationStatus.Archived);
        organization.IsActive.Should().BeFalse();
    }
}
