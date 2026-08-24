using BuildingBlocks.Application;
using ErrorOr;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Infrastructure.Database;
using Easebnb.Organization.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Easebnb.Organization.UnitTests.Services;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

public class OrganizationMemberServiceTests : IDisposable
{
    private readonly OrganizationDbContext _dbContext;
    private readonly OrganizationMemberService _sut;

    public OrganizationMemberServiceTests()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new OrganizationDbContext(options);
        _sut = new OrganizationMemberService(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private Organization SeedOrganization(Guid ownerId, bool archived = false)
    {
        var organization = Organization.Create("My Hotel", "my-hotel", null, ownerId);
        if (archived) organization.Archive();
        _dbContext.Organizations.Add(organization);
        _dbContext.SaveChanges();
        return organization;
    }

    private OrganizationMember SeedMembership(
        Guid organizationId,
        Guid userId,
        OrganizationMemberRole role,
        DateTime? joinedAt = null)
    {
        var member = OrganizationMember.Create(organizationId, userId, role);
        if (joinedAt is not null) member.CreatedAt = joinedAt.Value;
        _dbContext.OrganizationMembers.Add(member);
        _dbContext.SaveChanges();
        return member;
    }

    private RegisteredUser SeedRegisteredUser(Guid userId, string email = "user@test.com", string? userName = "user")
    {
        var registeredUser = RegisteredUser.Create(userId, email, userName);
        _dbContext.RegisteredUsers.Add(registeredUser);
        _dbContext.SaveChanges();
        return registeredUser;
    }

    /// <summary>Seeds an organization with an owner and returns (organization, ownerId).</summary>
    private (Organization Organization, Guid OwnerId) SeedOwnedOrganization(bool archived = false)
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization(ownerId, archived);
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);
        return (organization, ownerId);
    }

    // ---------------------------------------------------------------
    // GetMembersAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetMembersAsync_WhenCalledByMember_ReturnsProjectionEnrichedMembersOrderedByJoinedAt()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var projectedUserId = Guid.NewGuid();
        var unprojectedUserId = Guid.NewGuid();
        SeedMembership(organization.Id, projectedUserId, OrganizationMemberRole.Member, DateTime.UtcNow.AddDays(1));
        SeedMembership(organization.Id, unprojectedUserId, OrganizationMemberRole.Manager, DateTime.UtcNow.AddDays(2));
        SeedRegisteredUser(projectedUserId, "member@test.com", "member");

        var result = await _sut.GetMembersAsync(
            organization.Id, ownerId, new PagedRequest { Page = 1, PageSize = 10 });

        result.IsError.Should().BeFalse();
        var items = result.Value.Data.Items;
        items.Should().HaveCount(3);
        items.Select(m => m.UserId).Should().ContainInOrder(
            ownerId, projectedUserId, unprojectedUserId);

        var projected = items.Single(m => m.UserId == projectedUserId);
        projected.DisplayName.Should().Be("member");
        projected.Email.Should().Be("member@test.com");
        projected.Role.Should().Be("Member");

        var unprojected = items.Single(m => m.UserId == unprojectedUserId);
        unprojected.DisplayName.Should().BeNull(
            "members without a registered-user projection must show no display fields");
        unprojected.Email.Should().BeNull();
    }

    [Fact]
    public async Task GetMembersAsync_WhenSecondPageRequested_ReturnsPaginationMetadata()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        SeedMembership(organization.Id, Guid.NewGuid(), OrganizationMemberRole.Member);
        SeedMembership(organization.Id, Guid.NewGuid(), OrganizationMemberRole.Member);

        var result = await _sut.GetMembersAsync(
            organization.Id, ownerId, new PagedRequest { Page = 2, PageSize = 2 });

        result.Value.Data.Items.Should().HaveCount(1);
        result.Value.Data.Pagination.TotalItems.Should().Be(3);
        result.Value.Data.Pagination.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetMembersAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();

        var result = await _sut.GetMembersAsync(
            organization.Id, Guid.NewGuid(), new PagedRequest { Page = 1, PageSize = 10 });

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetMembersAsync_WhenOrganizationDoesNotExist_ReturnsNotFound()
    {
        var result = await _sut.GetMembersAsync(
            Guid.NewGuid(), Guid.NewGuid(), new PagedRequest { Page = 1, PageSize = 10 });

        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    // ---------------------------------------------------------------
    // AddMemberAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task AddMemberAsync_WhenCalledByAdmin_PersistsMembershipAndReturnsEnrichedResponse()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var adminId = Guid.NewGuid();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        var newUserId = Guid.NewGuid();
        SeedRegisteredUser(newUserId, "new@test.com", "new-user");

        var result = await _sut.AddMemberAsync(
            organization.Id, adminId, new AddOrganizationMemberRequest(newUserId, "Manager"));

        result.IsError.Should().BeFalse();
        var membership = await _dbContext.OrganizationMembers.AsNoTracking()
            .SingleAsync(m => m.UserId == newUserId);
        membership.OrganizationId.Should().Be(organization.Id);
        membership.Role.Should().Be(OrganizationMemberRole.Manager);
        result.Value.DisplayName.Should().Be("new-user");
        result.Value.Email.Should().Be("new@test.com");
        result.Value.Role.Should().Be("Manager");
        result.Value.JoinedAt.Should().Be(membership.CreatedAt);
    }

    [Fact]
    public async Task AddMemberAsync_WhenRoleIsInvalid_ReturnsValidationError()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var newUserId = Guid.NewGuid();
        SeedRegisteredUser(newUserId);

        var result = await _sut.AddMemberAsync(
            organization.Id, ownerId, new AddOrganizationMemberRequest(newUserId, "SuperUser"));

        result.FirstError.Type.Should().Be(ErrorType.Validation);
        (await _dbContext.OrganizationMembers.AsNoTracking().CountAsync()).Should().Be(1,
            "only the seeded owner membership should exist");
    }

    [Fact]
    public async Task AddMemberAsync_WhenRoleIsOwner_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var newUserId = Guid.NewGuid();
        SeedRegisteredUser(newUserId);

        var result = await _sut.AddMemberAsync(
            organization.Id, ownerId, new AddOrganizationMemberRequest(newUserId, "Owner"));

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Description.Should().Contain("owner");
    }

    [Fact]
    public async Task AddMemberAsync_WhenUserIsNotRegistered_ReturnsNotFound()
    {
        var (organization, ownerId) = SeedOwnedOrganization();

        var result = await _sut.AddMemberAsync(
            organization.Id, ownerId, new AddOrganizationMemberRequest(Guid.NewGuid(), "Member"));

        result.FirstError.Type.Should().Be(ErrorType.NotFound);
        result.FirstError.Description.Should().Be("User not found");
    }

    [Fact]
    public async Task AddMemberAsync_WhenUserIsAlreadyMember_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var existingMemberId = Guid.NewGuid();
        SeedMembership(organization.Id, existingMemberId, OrganizationMemberRole.Member);
        SeedRegisteredUser(existingMemberId);

        var result = await _sut.AddMemberAsync(
            organization.Id, ownerId, new AddOrganizationMemberRequest(existingMemberId, "Admin"));

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task AddMemberAsync_WhenCalledByManager_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var managerId = Guid.NewGuid();
        SeedMembership(organization.Id, managerId, OrganizationMemberRole.Manager);
        var newUserId = Guid.NewGuid();
        SeedRegisteredUser(newUserId);

        var result = await _sut.AddMemberAsync(
            organization.Id, managerId, new AddOrganizationMemberRequest(newUserId, "Member"));

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task AddMemberAsync_WhenOrganizationArchived_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization(archived: true);
        var newUserId = Guid.NewGuid();
        SeedRegisteredUser(newUserId);

        var result = await _sut.AddMemberAsync(
            organization.Id, ownerId, new AddOrganizationMemberRequest(newUserId, "Member"));

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    // ---------------------------------------------------------------
    // ChangeMemberRoleAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAdminPromotesMember_PersistsNewRole()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var adminId = Guid.NewGuid();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        var targetId = Guid.NewGuid();
        var target = SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, adminId, targetId, new UpdateOrganizationMemberRoleRequest("Manager"));

        result.IsError.Should().BeFalse();
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.Id == target.Id))
            .Role.Should().Be(OrganizationMemberRole.Manager);
        result.Value.Role.Should().Be("Manager");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenRoleUnchanged_ReturnsValidationError()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var targetId = Guid.NewGuid();
        SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, ownerId, targetId, new UpdateOrganizationMemberRoleRequest("Member"));

        result.FirstError.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenTargetIsNotMember_ReturnsNotFound()
    {
        var (organization, ownerId) = SeedOwnedOrganization();

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, ownerId, Guid.NewGuid(), new UpdateOrganizationMemberRoleRequest("Manager"));

        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenTargetIsOwner_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization();

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, ownerId, ownerId, new UpdateOrganizationMemberRoleRequest("Admin"));

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Description.Should().Contain("transfer ownership");
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.UserId == ownerId))
            .Role.Should().Be(OrganizationMemberRole.Owner, "the owner must not be demoted directly");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenOwnerGrantsOwnerRole_TransfersOwnershipAtomically()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var targetId = Guid.NewGuid();
        var target = SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, ownerId, targetId, new UpdateOrganizationMemberRoleRequest("Owner"));

        result.IsError.Should().BeFalse();
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.Id == target.Id))
            .Role.Should().Be(OrganizationMemberRole.Owner, "the target becomes the new owner");
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.UserId == ownerId))
            .Role.Should().Be(OrganizationMemberRole.Admin, "the previous owner is demoted to admin");
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).OwnerUserId
            .Should().Be(targetId, "the denormalized owner reference moves with the transfer");
        result.Value.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAdminGrantsOwnerRole_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var adminId = Guid.NewGuid();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        var targetId = Guid.NewGuid();
        var target = SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, adminId, targetId, new UpdateOrganizationMemberRoleRequest("Owner"));

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.Id == target.Id))
            .Role.Should().Be(OrganizationMemberRole.Member);
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).OwnerUserId
            .Should().NotBe(targetId);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAdminModifiesAdmin_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var actorId = Guid.NewGuid();
        SeedMembership(organization.Id, actorId, OrganizationMemberRole.Admin);
        var otherAdminId = Guid.NewGuid();
        var otherAdmin = SeedMembership(organization.Id, otherAdminId, OrganizationMemberRole.Admin);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, actorId, otherAdminId, new UpdateOrganizationMemberRoleRequest("Member"));

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.Id == otherAdmin.Id))
            .Role.Should().Be(OrganizationMemberRole.Admin);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenAdminGrantsAdminRole_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var adminId = Guid.NewGuid();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        var targetId = Guid.NewGuid();
        var target = SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, adminId, targetId, new UpdateOrganizationMemberRoleRequest("Admin"));

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.Id == target.Id))
            .Role.Should().Be(OrganizationMemberRole.Member);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenMemberTriesToChangeRoles_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var actorId = Guid.NewGuid();
        SeedMembership(organization.Id, actorId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, actorId, actorId, new UpdateOrganizationMemberRoleRequest("Admin"));

        result.FirstError.Type.Should().Be(ErrorType.Forbidden,
            "a plain member must not be able to elevate their own role");
        (await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync(m => m.UserId == actorId))
            .Role.Should().Be(OrganizationMemberRole.Member);
    }

    [Fact]
    public async Task ChangeMemberRoleAsync_WhenOrganizationArchived_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization(archived: true);
        var targetId = Guid.NewGuid();
        SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.ChangeMemberRoleAsync(
            organization.Id, ownerId, targetId, new UpdateOrganizationMemberRoleRequest("Manager"));

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    // ---------------------------------------------------------------
    // RemoveMemberAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task RemoveMemberAsync_WhenOwnerRemovesMember_PersistsRemoval()
    {
        var (organization, ownerId) = SeedOwnedOrganization();
        var targetId = Guid.NewGuid();
        SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.RemoveMemberAsync(organization.Id, ownerId, targetId);

        result.IsError.Should().BeFalse();
        (await _dbContext.OrganizationMembers.AsNoTracking()
            .AnyAsync(m => m.UserId == targetId)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenTargetIsOwner_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization();

        var result = await _sut.RemoveMemberAsync(organization.Id, ownerId, ownerId);

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Description.Should().Contain("transfer ownership");
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenAdminRemovesAdmin_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var adminId = Guid.NewGuid();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        var otherAdminId = Guid.NewGuid();
        SeedMembership(organization.Id, otherAdminId, OrganizationMemberRole.Admin);

        var result = await _sut.RemoveMemberAsync(organization.Id, adminId, otherAdminId);

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenAdminRemovesMember_Succeeds()
    {
        var (organization, _) = SeedOwnedOrganization();
        var adminId = Guid.NewGuid();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        var targetId = Guid.NewGuid();
        SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.RemoveMemberAsync(organization.Id, adminId, targetId);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenTargetIsNotMember_ReturnsNotFound()
    {
        var (organization, ownerId) = SeedOwnedOrganization();

        var result = await _sut.RemoveMemberAsync(organization.Id, ownerId, Guid.NewGuid());

        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        var (organization, _) = SeedOwnedOrganization();
        var outsiderId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.RemoveMemberAsync(organization.Id, outsiderId, targetId);

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task RemoveMemberAsync_WhenOrganizationArchived_ReturnsConflict()
    {
        var (organization, ownerId) = SeedOwnedOrganization(archived: true);
        var targetId = Guid.NewGuid();
        SeedMembership(organization.Id, targetId, OrganizationMemberRole.Member);

        var result = await _sut.RemoveMemberAsync(organization.Id, ownerId, targetId);

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }
}
