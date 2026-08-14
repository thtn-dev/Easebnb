namespace BuildingBlocks.SharedKernel.Common;

/// <summary>
///     Base entity interface
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IEntityBase<T>
{
    T Id { get; set; }
}

/// <summary>
///     Auditable entity interface
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}

/// <summary>
///     Soft delete interface
/// </summary>
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}

/// <summary>
///     Aggregate root interface
/// </summary>
public interface IAggregateRoot
{
}

/// <summary>
///     Base entity class
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class EntityBase<T> : IEntityBase<T>
    where T : IEquatable<T>
{
    public T Id { get; set; } = default!;
}

/// <summary>
///     Base entity aggregate class
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class AggregateRoot<T> : EntityBase<T>, IAggregateRoot, IHasDomainEvents
    where T : IEquatable<T>
{
    private List<IDomainEvent>? _domainEvents;

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
    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents ??= [];

        _domainEvents.Add(domainEvent);
    }
}

public static class EntityExtensions
{
    public static string? GetEntityIdName(this Type type)
    {
        if (!type.IsSubclassOf(typeof(EntityBase<>))) return null;
        var idProperty = type.GetProperty("Id");
        if (idProperty == null) return null;
        var prefix = type.Name.Replace("Entity", "");
        return prefix + idProperty.Name;
    }
}