using BuildingBlocks.SharedKernel.Common;
using Microsoft.EntityFrameworkCore;

namespace Easebnb.Database.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyAuditableConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IAuditableEntity).IsAssignableFrom(entityType.ClrType)) continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IAuditableEntity.CreatedAt))
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAdd();

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(IAuditableEntity.UpdatedAt))
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .ValueGeneratedOnAddOrUpdate();
        }
    }
}