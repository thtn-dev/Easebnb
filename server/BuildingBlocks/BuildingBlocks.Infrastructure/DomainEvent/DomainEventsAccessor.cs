using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Infrastructure.DomainEvent;

public sealed class DomainEventsAccessor<TContext>(TContext dbContext) : IDomainEventsAccessor
    where TContext : DbContext
{
    public IReadOnlyCollection<IDomainEvent> GetAllDomainEvents()
    {
        var domainEntities = dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Count != 0).ToList();

        return [.. domainEntities.SelectMany(x => x.Entity.DomainEvents ?? [])];
    }

    public void ClearAllDomainEvents()
    {
        var domainEntities = dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Where(x => x.Entity.DomainEvents != null && x.Entity.DomainEvents.Count != 0).ToList();

        domainEntities
            .ForEach(entity => entity.Entity.ClearDomainEvents());
    }
}