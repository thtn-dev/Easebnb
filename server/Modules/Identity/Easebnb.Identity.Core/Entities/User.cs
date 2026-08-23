using BuildingBlocks.SharedKernel;
using BuildingBlocks.SharedKernel.Common;
using Microsoft.AspNetCore.Identity;

namespace Easebnb.Identity.Core.Entities;

public class User : IdentityUser<Guid>, IEntityBase<Guid>, IAggregateRoot, IHasDomainEvents, IAuditableEntity
{
    public string? ProfilePictureKey { get; set; }

    private List<IDomainEvent>? _domainEvents;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public override Guid Id { get; set; }

    /// <summary>
    ///     Domain events occurred.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent>? DomainEvents => _domainEvents?.AsReadOnly();

    /// <summary>
    ///     Clear domain events.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents?.Clear();
    }

    /// <summary>
    ///     Add domain event.
    /// </summary>
    /// <param name="domainEvent">Domain event.</param>
    public void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents ??= [];

        _domainEvents.Add(domainEvent);
    }
}