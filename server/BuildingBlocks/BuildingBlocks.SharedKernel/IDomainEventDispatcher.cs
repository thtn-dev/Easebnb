namespace BuildingBlocks.SharedKernel;

public interface IDomainEventDispatcher
{
    Task DispatchDomainEventsAsync(CancellationToken cancellationToken = default);
}