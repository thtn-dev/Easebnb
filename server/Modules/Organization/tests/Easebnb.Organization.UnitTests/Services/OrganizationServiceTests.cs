using BuildingBlocks.Application;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using BuildingBlocks.SharedKernel;
using ErrorOr;
using Easebnb.Organization.Core.Dtos;
using Easebnb.Organization.Core.Entities;
using Easebnb.Organization.Infrastructure.Database;
using Easebnb.Organization.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Easebnb.Organization.UnitTests.Services;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

public class OrganizationServiceTests : IDisposable
{
    private const string LogoBucket = "easebnb-organizations";

    private readonly OrganizationDbContext _dbContext;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IObjectStorage> _objectStorageMock;
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        _dbContext = CreateContext();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        // Mirror the real UnitOfWork<TDbContext>: committing saves the
        // tracked changes, so persistence can be asserted after Create.
        _unitOfWorkMock
            .Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => _dbContext.SaveChangesAsync(ct));
        _objectStorageMock = new Mock<IObjectStorage>();
        _sut = new OrganizationService(
            _dbContext,
            _unitOfWorkMock.Object,
            _objectStorageMock.Object,
            NullLogger<OrganizationService>.Instance);
    }

    public void Dispose() => _dbContext.Dispose();

    /// <summary>DbContext whose SaveChangesAsync can be made to fail per-test.</summary>
    private sealed class ThrowingSaveChangesContext(DbContextOptions<OrganizationDbContext> options)
        : OrganizationDbContext(options)
    {
        public bool ThrowOnSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return ThrowOnSave
                ? throw new DbUpdateException("simulated database failure")
                : base.SaveChangesAsync(cancellationToken);
        }
    }

    private static OrganizationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OrganizationDbContext(options);
    }

    private Organization SeedOrganization(
        string slug = "my-hotel",
        Guid? ownerId = null,
        DateTime? createdAt = null,
        bool archived = false,
        string? logoKey = null)
    {
        var organization = Organization.Create("My Hotel", slug, null, ownerId ?? Guid.NewGuid());
        if (createdAt is not null) organization.CreatedAt = createdAt.Value;
        if (archived) organization.Archive();
        if (logoKey is not null) organization.SetLogo(logoKey);
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

    private void SetupSuccessfulUpload()
    {
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Returns((PutObjectRequest request, CancellationToken _) =>
                Task.FromResult(new PutObjectResult { Bucket = request.Bucket, Key = request.Key }));
    }

    // ---------------------------------------------------------------
    // CreateOrganizationAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateOrganizationAsync_WhenSlugProvided_PersistsOrganizationWithOwnerMembership()
    {
        var userId = Guid.NewGuid();
        var request = new CreateOrganizationRequest("My Hotel", "my-hotel", "A boutique hotel");

        var result = await _sut.CreateOrganizationAsync(userId, request);

        result.IsError.Should().BeFalse();
        var organization = await _dbContext.Organizations.AsNoTracking().SingleAsync();
        organization.Name.Should().Be("My Hotel");
        organization.Slug.Should().Be("my-hotel");
        organization.Description.Should().Be("A boutique hotel");
        organization.OwnerUserId.Should().Be(userId);
        organization.Status.Should().Be(OrganizationStatus.Active);

        var membership = await _dbContext.OrganizationMembers.AsNoTracking().SingleAsync();
        membership.OrganizationId.Should().Be(organization.Id);
        membership.UserId.Should().Be(userId);
        membership.Role.Should().Be(OrganizationMemberRole.Owner);

        result.Value.Id.Should().Be(organization.Id);
        result.Value.OwnerUserId.Should().Be(userId);
        result.Value.Status.Should().Be("Active");

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrganizationAsync_WhenSlugOmitted_DerivesSlugFromName()
    {
        var request = new CreateOrganizationRequest("Sơn Trà Hotel", null, null);

        var result = await _sut.CreateOrganizationAsync(Guid.NewGuid(), request);

        result.IsError.Should().BeFalse();
        result.Value.Slug.Should().Be("son-tra-hotel");
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).Slug
            .Should().Be("son-tra-hotel");
    }

    [Fact]
    public async Task CreateOrganizationAsync_WhenSlugAlreadyTaken_ReturnsConflictWithoutOpeningTransaction()
    {
        SeedOrganization(slug: "taken");
        var request = new CreateOrganizationRequest("My Hotel", "taken", null);

        var result = await _sut.CreateOrganizationAsync(Guid.NewGuid(), request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Description.Should().Contain("already exists");
        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateOrganizationAsync_WhenCommitFails_RollsBackAndPropagates()
    {
        _unitOfWorkMock
            .Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var request = new CreateOrganizationRequest("My Hotel", "my-hotel", null);

        var act = () => _sut.CreateOrganizationAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWorkMock.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        (await _dbContext.Organizations.AsNoTracking().CountAsync()).Should().Be(0,
            "nothing must be persisted when the transaction fails");
        (await _dbContext.OrganizationMembers.AsNoTracking().CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------
    // GetOrganizationByIdAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenUserIsMember_ReturnsOrganization()
    {
        var userId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, userId, OrganizationMemberRole.Member);

        var result = await _sut.GetOrganizationByIdAsync(organization.Id, userId);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(organization.Id);
        result.Value.Slug.Should().Be("my-hotel");
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenOrganizationDoesNotExist_ReturnsNotFound()
    {
        var result = await _sut.GetOrganizationByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetOrganizationByIdAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        var organization = SeedOrganization();

        var result = await _sut.GetOrganizationByIdAsync(organization.Id, Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    // ---------------------------------------------------------------
    // GetOrganizationBySlugAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetOrganizationBySlugAsync_WhenSlugDiffersInCaseAndWhitespace_ReturnsOrganization()
    {
        var userId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, userId, OrganizationMemberRole.Admin);

        var result = await _sut.GetOrganizationBySlugAsync("  MY-HOTEL ", userId);

        result.IsError.Should().BeFalse();
        result.Value.Id.Should().Be(organization.Id);
    }

    [Fact]
    public async Task GetOrganizationBySlugAsync_WhenOrganizationDoesNotExist_ReturnsNotFound()
    {
        var result = await _sut.GetOrganizationBySlugAsync("missing", Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetOrganizationBySlugAsync_WhenUserIsNotMember_ReturnsForbidden()
    {
        SeedOrganization();

        var result = await _sut.GetOrganizationBySlugAsync("my-hotel", Guid.NewGuid());

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    // ---------------------------------------------------------------
    // GetMyOrganizationsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetMyOrganizationsAsync_WhenUserHasMemberships_ReturnsOnlyTheirOrganizationsWithRoles()
    {
        var userId = Guid.NewGuid();
        var older = SeedOrganization(slug: "older", createdAt: DateTime.UtcNow.AddDays(-2));
        var newer = SeedOrganization(slug: "newer", createdAt: DateTime.UtcNow);
        SeedMembership(older.Id, userId, OrganizationMemberRole.Member);
        SeedMembership(newer.Id, userId, OrganizationMemberRole.Owner);
        SeedMembership(SeedOrganization(slug: "someone-elses").Id, Guid.NewGuid(), OrganizationMemberRole.Owner);

        var result = await _sut.GetMyOrganizationsAsync(userId, new PagedRequest { Page = 1, PageSize = 10 });

        result.IsError.Should().BeFalse();
        result.Value.Data.Items.Should().HaveCount(2);
        result.Value.Data.Items[0].Id.Should().Be(newer.Id, "organizations are listed newest first");
        result.Value.Data.Items[1].Id.Should().Be(older.Id);
        result.Value.Data.Items[1].Role.Should().Be("Member");
        result.Value.Data.Items.Should().NotContain(o => o.Slug == "someone-elses");
    }

    [Fact]
    public async Task GetMyOrganizationsAsync_WhenSecondPageRequested_ReturnsPaginationMetadata()
    {
        var userId = Guid.NewGuid();
        foreach (var slug in new[] { "org-1", "org-2", "org-3" })
            SeedMembership(SeedOrganization(slug: slug).Id, userId, OrganizationMemberRole.Member);

        var result = await _sut.GetMyOrganizationsAsync(userId, new PagedRequest { Page = 2, PageSize = 2 });

        result.Value.Data.Items.Should().HaveCount(1);
        result.Value.Data.Pagination.CurrentPage.Should().Be(2);
        result.Value.Data.Pagination.TotalItems.Should().Be(3);
        result.Value.Data.Pagination.TotalPages.Should().Be(2);
        result.Value.Data.Pagination.HasNextPage.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // UpdateOrganizationAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateOrganizationAsync_WhenCalledByOwner_PersistsNewDetails()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);
        var request = new UpdateOrganizationRequest("New Name", "new-slug", "New description");

        var result = await _sut.UpdateOrganizationAsync(organization.Id, ownerId, request);

        result.IsError.Should().BeFalse();
        var persisted = await _dbContext.Organizations.AsNoTracking().SingleAsync();
        persisted.Name.Should().Be("New Name");
        persisted.Slug.Should().Be("new-slug");
        persisted.Description.Should().Be("New description");
        result.Value.Slug.Should().Be("new-slug");
    }

    [Fact]
    public async Task UpdateOrganizationAsync_WhenSlugUnchanged_ReturnsSuccessWithoutConflict()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);
        var request = new UpdateOrganizationRequest("Renamed", "  MY-HOTEL ", null);

        var result = await _sut.UpdateOrganizationAsync(organization.Id, ownerId, request);

        result.IsError.Should().BeFalse();
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).Name
            .Should().Be("Renamed");
    }

    [Fact]
    public async Task UpdateOrganizationAsync_WhenNewSlugAlreadyTaken_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);
        SeedOrganization(slug: "taken");
        var request = new UpdateOrganizationRequest("New Name", "taken", null);

        var result = await _sut.UpdateOrganizationAsync(organization.Id, ownerId, request);

        result.IsError.Should().BeTrue();
        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        (await _dbContext.Organizations.AsNoTracking().SingleAsync(o => o.Id == organization.Id)).Name
            .Should().Be("My Hotel", "the update must not be applied");
    }

    [Fact]
    public async Task UpdateOrganizationAsync_WhenCalledByMember_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, userId, OrganizationMemberRole.Member);
        var request = new UpdateOrganizationRequest("New Name", "new-slug", null);

        var result = await _sut.UpdateOrganizationAsync(organization.Id, userId, request);

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task UpdateOrganizationAsync_WhenOrganizationArchived_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization(archived: true);
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);

        var result = await _sut.UpdateOrganizationAsync(
            organization.Id, ownerId, new UpdateOrganizationRequest("New Name", "new-slug", null));

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
        result.FirstError.Description.Should().Be("Organization is not active");
    }

    [Fact]
    public async Task UpdateOrganizationAsync_WhenOrganizationDoesNotExist_ReturnsNotFound()
    {
        var result = await _sut.UpdateOrganizationAsync(
            Guid.NewGuid(), Guid.NewGuid(), new UpdateOrganizationRequest("New Name", "new-slug", null));

        result.FirstError.Type.Should().Be(ErrorType.NotFound);
    }

    // ---------------------------------------------------------------
    // ArchiveOrganizationAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task ArchiveOrganizationAsync_WhenCalledByOwner_ArchivesOrganization()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);

        var result = await _sut.ArchiveOrganizationAsync(organization.Id, ownerId);

        result.IsError.Should().BeFalse();
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).Status
            .Should().Be(OrganizationStatus.Archived);
    }

    [Fact]
    public async Task ArchiveOrganizationAsync_WhenCalledByAdmin_ReturnsForbidden()
    {
        var adminId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);

        var result = await _sut.ArchiveOrganizationAsync(organization.Id, adminId);

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).Status
            .Should().Be(OrganizationStatus.Active);
    }

    [Fact]
    public async Task ArchiveOrganizationAsync_WhenAlreadyArchived_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization(archived: true);
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);

        var result = await _sut.ArchiveOrganizationAsync(organization.Id, ownerId);

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }

    // ---------------------------------------------------------------
    // UpdateLogoAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateLogoAsync_WhenCalledByAdmin_UploadsLogoPersistsKeyAndReplacesOldObject()
    {
        var adminId = Guid.NewGuid();
        var organization = SeedOrganization(logoKey: "organizations/old/logo/old.jpg");
        SeedMembership(organization.Id, adminId, OrganizationMemberRole.Admin);
        SetupSuccessfulUpload();

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _sut.UpdateLogoAsync(organization.Id, adminId, content);

        result.IsError.Should().BeFalse();
        _objectStorageMock.Verify(
            s => s.PutAsync(
                It.Is<PutObjectRequest>(r =>
                    r.Bucket == LogoBucket &&
                    r.Key.StartsWith($"organizations/{organization.Id}/logo/")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        var persisted = await _dbContext.Organizations.AsNoTracking().SingleAsync();
        persisted.LogoKey.Should().NotBeNullOrEmpty().And.NotBe("organizations/old/logo/old.jpg");
        result.Value.LogoKey.Should().Be(persisted.LogoKey);
        _objectStorageMock.Verify(
            s => s.DeleteAsync(LogoBucket, "organizations/old/logo/old.jpg", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateLogoAsync_WhenStreamIsEmpty_ReturnsValidationError()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);

        using var content = new MemoryStream();
        var result = await _sut.UpdateLogoAsync(organization.Id, ownerId, content);

        result.FirstError.Type.Should().Be(ErrorType.Validation);
        _objectStorageMock.Verify(
            s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateLogoAsync_WhenUploadFails_ReturnsUnexpectedError()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization(logoKey: "organizations/old/logo/old.jpg");
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);
        _objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ObjectStorageException(ObjectStorageErrorCode.UploadFailed, "upload failed"));

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _sut.UpdateLogoAsync(organization.Id, ownerId, content);

        result.FirstError.Type.Should().Be(ErrorType.Unexpected);
        (await _dbContext.Organizations.AsNoTracking().SingleAsync()).LogoKey
            .Should().Be("organizations/old/logo/old.jpg", "the old logo must be kept");
    }

    [Fact]
    public async Task UpdateLogoAsync_WhenDatabaseUpdateFails_DeletesNewObjectAndKeepsOld()
    {
        var dbContext = new ThrowingSaveChangesContext(
            new DbContextOptionsBuilder<OrganizationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var objectStorageMock = new Mock<IObjectStorage>();
        PutObjectRequest? putRequest = null;
        objectStorageMock
            .Setup(s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback((PutObjectRequest request, CancellationToken _) => putRequest = request)
            .Returns((PutObjectRequest request, CancellationToken _) =>
                Task.FromResult(new PutObjectResult { Bucket = request.Bucket, Key = request.Key }));
        var sut = new OrganizationService(
            dbContext,
            new Mock<IUnitOfWork>().Object,
            objectStorageMock.Object,
            NullLogger<OrganizationService>.Instance);
        var ownerId = Guid.NewGuid();
        var organization = Organization.Create("My Hotel", "my-hotel", null, ownerId);
        organization.SetLogo("organizations/old/logo/old.jpg");
        dbContext.Organizations.Add(organization);
        dbContext.OrganizationMembers.Add(OrganizationMember.Create(organization.Id, ownerId, OrganizationMemberRole.Owner));
        dbContext.SaveChanges();
        dbContext.ThrowOnSave = true;

        try
        {
            using var content = new MemoryStream(new byte[] { 1, 2, 3 });
            var act = () => sut.UpdateLogoAsync(organization.Id, ownerId, content);

            await act.Should().ThrowAsync<DbUpdateException>();
            objectStorageMock.Verify(
                s => s.DeleteAsync(LogoBucket, putRequest!.Key, It.IsAny<CancellationToken>()),
                Times.Once, "the newly uploaded object must be removed again");
            objectStorageMock.Verify(
                s => s.DeleteAsync(LogoBucket, "organizations/old/logo/old.jpg", It.IsAny<CancellationToken>()),
                Times.Never, "the old logo must not be deleted when the update failed");
        }
        finally
        {
            await dbContext.DisposeAsync();
        }
    }

    [Fact]
    public async Task UpdateLogoAsync_WhenCalledByMember_ReturnsForbidden()
    {
        var memberId = Guid.NewGuid();
        var organization = SeedOrganization();
        SeedMembership(organization.Id, memberId, OrganizationMemberRole.Member);

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _sut.UpdateLogoAsync(organization.Id, memberId, content);

        result.FirstError.Type.Should().Be(ErrorType.Forbidden);
        _objectStorageMock.Verify(
            s => s.PutAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateLogoAsync_WhenOrganizationArchived_ReturnsConflict()
    {
        var ownerId = Guid.NewGuid();
        var organization = SeedOrganization(archived: true);
        SeedMembership(organization.Id, ownerId, OrganizationMemberRole.Owner);

        using var content = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await _sut.UpdateLogoAsync(organization.Id, ownerId, content);

        result.FirstError.Type.Should().Be(ErrorType.Conflict);
    }
}
