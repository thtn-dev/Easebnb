namespace BuildingBlocks.SharedKernel;

public interface IUnitOfWork
{
    bool HasActiveTransaction { get; }
    Task<int> SaveEntitiesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}