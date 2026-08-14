using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Infrastructure;

public sealed class UnitOfWork<TDbContext>(
    TDbContext dbContext,
    IDomainEventDispatcher dispatcher,
    ILogger<UnitOfWork<TDbContext>> logger)
    : IUnitOfWork, IAsyncDisposable
    where TDbContext : DbContext
{
    private IDbContextTransaction? _currentTransaction;

    public async ValueTask DisposeAsync()
    {
        if (_currentTransaction is not null)
        {
            logger.LogWarning(
                "UnitOfWork disposed with active transaction. Rolling back.");

            await RollbackInternalAsync(CancellationToken.None);
        }
    }

    public bool HasActiveTransaction => _currentTransaction is not null;

    #region SaveChanges

    public async Task<int> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        return await SaveChangesInternalAsync(cancellationToken);
    }

    private async Task<int> SaveChangesInternalAsync(CancellationToken cancellationToken)
    {
        await dispatcher.DispatchDomainEventsAsync(cancellationToken);
        var result = await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    #endregion

    #region Transaction

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null) return;

        _currentTransaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        logger.LogDebug(
            "Started transaction {TransactionId}",
            _currentTransaction.TransactionId);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException(
                "No active transaction. Call BeginTransactionAsync first.");

        try
        {
            await SaveChangesInternalAsync(cancellationToken);

            await _currentTransaction.CommitAsync(cancellationToken);

            logger.LogDebug(
                "Committed transaction {TransactionId}",
                _currentTransaction.TransactionId);
        }
        catch
        {
            await RollbackInternalAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        await RollbackInternalAsync(cancellationToken);
    }

    private async Task RollbackInternalAsync(
        CancellationToken cancellationToken)
    {
        if (_currentTransaction is null)
            return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);

            logger.LogWarning(
                "Rolled back transaction {TransactionId}",
                _currentTransaction.TransactionId);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction is null)
            return;

        await _currentTransaction.DisposeAsync();

        _currentTransaction = null;
    }

    #endregion
}