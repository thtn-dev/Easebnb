using Easebnb.Database.Interceptors;
using BuildingBlocks.SharedKernel.Common;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.UnitTests;

public class AuditableEntityInterceptorTests
{
    public sealed class AuditableEntity : IEntityBase<Guid>, IAuditableEntity, ISoftDelete
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
    {
        public DbSet<AuditableEntity> Entities => Set<AuditableEntity>();
    }

    private static AuditDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new AuditableEntityInterceptor())
            .Options;
        return new AuditDbContext(options);
    }


    // ---------------------------------------------------------------
    // Added entities
    // ---------------------------------------------------------------

    [Fact]
    public void SaveChanges_WhenAddedEntityStillHasEmptyId_AssignsVersion7IdAndAuditStamps()
    {
        using var context = CreateContext();
        var entity = new AuditableEntity { Name = "new" };
        context.Entities.Add(entity);
        // EF convention assigns a temporary key value on Add; reset it so the
        // interceptor's empty-Id branch is the one under test.
        context.Entry(entity).Property(e => e.Id).CurrentValue = Guid.Empty;
        var before = DateTime.UtcNow.AddSeconds(-5);

        context.SaveChanges();

        entity.Id.Should().NotBeEmpty("the interceptor must assign the primary key");
        entity.Id.Version.Should().Be(7, "ids must be time-ordered GUID v7 values");
        entity.CreatedAt.Should().BeOnOrAfter(before);
        entity.UpdatedAt.Should().Be(entity.CreatedAt, "creation sets both stamps to the same instant");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityAdded_SetsAuditStamps()
    {
        using var context = CreateContext();
        var entity = new AuditableEntity { Id = Guid.NewGuid(), Name = "new" };
        context.Entities.Add(entity);

        await context.SaveChangesAsync();

        entity.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-1));
        entity.UpdatedAt.Should().Be(entity.CreatedAt);
    }

    // ---------------------------------------------------------------
    // Modified entities
    // ---------------------------------------------------------------

    [Fact]
    public void SaveChanges_WhenEntityModified_UpdatesUpdatedAtAndPreservesCreatedAt()
    {
        using var context = CreateContext();
        var entity = new AuditableEntity { Name = "original" };
        context.Entities.Add(entity);
        context.SaveChanges();
        var storedCreatedAt = entity.CreatedAt;

        entity.Name = "changed";
        entity.CreatedAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        context.SaveChanges();

        var reloaded = context.Entities.AsNoTracking().Single();
        reloaded.Name.Should().Be("changed");
        reloaded.CreatedAt.Should().Be(storedCreatedAt,
            "a modification attempt on CreatedAt must not reach the store");
        reloaded.UpdatedAt.Should().BeOnOrAfter(storedCreatedAt);
    }

    // ---------------------------------------------------------------
    // Deleted entities (soft delete)
    // ---------------------------------------------------------------

    [Fact]
    public void SaveChanges_WhenEntityDeleted_ConvertsToSoftDelete()
    {
        using var context = CreateContext();
        var entity = new AuditableEntity { Name = "doomed" };
        context.Entities.Add(entity);
        context.SaveChanges();

        context.Entities.Remove(entity);
        context.SaveChanges();

        var reloaded = context.Entities.AsNoTracking().Single();
        reloaded.IsDeleted.Should().BeTrue("deletes must become soft deletes");
        reloaded.DeletedAt.Should().NotBeNull();
        reloaded.Name.Should().Be("doomed", "the row must still exist in the store");
    }
}
