namespace Easebnb.Organization.Infrastructure.Database;

using Easebnb.Database.Extensions;
using Easebnb.Organization.Core.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;

// 'Organization' (the entity) loses to the enclosing 'Easebnb.Organization'
// namespace segment during name lookup, so alias it explicitly.
using Organization = Easebnb.Organization.Core.Entities.Organization;

public class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<RegisteredUser> RegisteredUsers => Set<RegisteredUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organization");

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("organizations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.LogoKey).HasMaxLength(500);
            entity.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
            // Logical reference to the Identity module's user; no FK can
            // cross module schemas, integrity is enforced in the application
            // layer via the registered_users projection.
            entity.Property(e => e.OwnerUserId).IsRequired();

            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.OwnerUserId);
        });

        modelBuilder.Entity<OrganizationMember>(entity =>
        {
            entity.ToTable("organization_members");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasConversion<string>().HasMaxLength(20);

            entity.HasOne<Organization>()
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // One membership per user per organization; also serves as the
            // index for "all members of an organization".
            entity.HasIndex(e => new { e.OrganizationId, e.UserId }).IsUnique();

            // "All organizations of a user" lookups.
            entity.HasIndex(e => e.UserId);

            // Single-owner invariant: at most one Owner membership per
            // organization (partial unique index, PostgreSQL).
            entity.HasIndex(e => e.OrganizationId)
                .HasDatabaseName("ix_organization_members_owner")
                .HasFilter("\"role\" = 'Owner'")
                .IsUnique();
        });

        modelBuilder.Entity<RegisteredUser>(entity =>
        {
            entity.ToTable("registered_users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);
        });

        modelBuilder.ApplyAuditableConventions();

        // MassTransit transactional outbox/inbox state (consumer side), kept
        // inside this module's own schema.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}
