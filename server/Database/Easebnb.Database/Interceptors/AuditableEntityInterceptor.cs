using BuildingBlocks.SharedKernel.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Easebnb.Database.Interceptors;

public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void UpdateEntities(DbContext? context)
    {
        if (context is null) return;
        var utcNow = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry is { State: EntityState.Added, Entity: IEntityBase<Guid> gen } && gen.Id == Guid.Empty)
                gen.Id = Guid.CreateVersion7(DateTimeOffset.UtcNow);

            if (entry.Entity is IAuditableEntity aud)
                switch (entry.State)
                {
                    case EntityState.Added:
                        aud.CreatedAt = utcNow;
                        aud.UpdatedAt = utcNow;
                        break;
                    case EntityState.Modified:
                        aud.UpdatedAt = utcNow;
                        entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                        break;
                    case EntityState.Detached:
                    case EntityState.Unchanged:
                    case EntityState.Deleted:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(context), entry.State, null);
                }

            if (entry is not { Entity: ISoftDelete soft, State: EntityState.Deleted }) continue;

            entry.State = EntityState.Modified;
            soft.IsDeleted = true;
            soft.DeletedAt = utcNow;
        }
    }
}