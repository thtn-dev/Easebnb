using BuildingBlocks.SharedKernel;
using BuildingBlocks.SharedKernel.Common;
using Microsoft.AspNetCore.Identity;

namespace TmsBase.Identity.Domain.Entities;

public class User : IdentityUser<Guid>, IEntityBase<Guid>, IAggregateRoot, IHasDomainEvents, IAuditableEntity
{
    public override Guid Id { get; set; }

    private List<IDomainEvent>? _domainEvents;
    /// <summary>
    /// Domain events occurred.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent>? DomainEvents => _domainEvents?.AsReadOnly();

    /// <summary>
    /// Clear domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    /// <summary>
    /// Add domain event.
    /// </summary>
    /// <param name="domainEvent">Domain event.</param>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents ??= [];

        this._domainEvents.Add(domainEvent);
    }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
