using MediatR;

namespace BuildingBlocks.SharedKernel;

public interface IDomainEvent : INotification
{
    Guid Id { get; }

    DateTimeOffset OccurredOn { get; }
}

public class DomainEventBase : IDomainEvent
{
    public DomainEventBase()
    {
        Id = Guid.NewGuid();
        OccurredOn = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    public DateTimeOffset OccurredOn { get; }
}

public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent>? DomainEvents { get; }

    void ClearDomainEvents();
}