using System.Reflection;
using Easebnb.Database.Extensions;
using Easebnb.Identity.Core.Entities;
using MassTransit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Easebnb.Identity.Infrastructure.Database;

public class AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options)
    : IdentityDbContext<User, Role, Guid>(options)
{
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<User>(entity => { entity.ToTable("users"); });

        modelBuilder.Entity<Role>(entity => { entity.ToTable("roles"); });

        modelBuilder.Entity<IdentityUserRole<Guid>>(entity => { entity.ToTable("user_roles"); });

        modelBuilder.Entity<IdentityUserClaim<Guid>>(entity => { entity.ToTable("user_claims"); });

        modelBuilder.Entity<IdentityUserLogin<Guid>>(entity => { entity.ToTable("user_logins"); });

        modelBuilder.Entity<IdentityRoleClaim<Guid>>(entity => { entity.ToTable("role_claims"); });

        modelBuilder.Entity<IdentityUserToken<Guid>>(entity => { entity.ToTable("user_tokens"); });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.CreatedByIp).HasMaxLength(45);
            entity.Property(e => e.RevokedByIp).HasMaxLength(45);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Token);
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.ApplyAuditableConventions();

        // MassTransit transactional outbox/inbox state, kept inside this
        // module's schema so the outbox moves with it if it is ever extracted.
        modelBuilder.AddTransactionalOutboxEntities();
    }
}