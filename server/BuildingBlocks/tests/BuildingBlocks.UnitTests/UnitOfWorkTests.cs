using BuildingBlocks.Infrastructure;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.UnitTests;

public class UnitOfWorkTests : IDisposable
{
    private readonly TestDbContext _context = CreateContext();
    private readonly Mock<IDomainEventDispatcher> _dispatcherMock = new();
    private readonly UnitOfWork<TestDbContext> _sut;

    public sealed class TestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> Entities => Set<TestEntity>();
    }

    public UnitOfWorkTests()
    {
        // Ignoring the warning makes the InMemory provider hand out a stub
        // transaction instead of throwing, so UnitOfWork's flow can run.
        _sut = new UnitOfWork<TestDbContext>(
            _context,
            _dispatcherMock.Object,
            NullLogger<UnitOfWork<TestDbContext>>.Instance);
    }

    private static TestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new TestDbContext(options);
    }

    public void Dispose()
    {
        _sut.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _context.Dispose();
    }


    // ---------------------------------------------------------------
    // SaveEntitiesAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task SaveEntitiesAsync_WhenCalled_DispatchesEventsBeforeSaving()
    {
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "test" };
        _context.Entities.Add(entity);
        var entryStatesAtDispatch = new List<EntityState>();
        _dispatcherMock
            .Setup(d => d.DispatchDomainEventsAsync(It.IsAny<CancellationToken>()))
            .Callback(() => entryStatesAtDispatch.Add(_context.Entry(entity).State));

        var result = await _sut.SaveEntitiesAsync(CancellationToken.None);

        result.Should().Be(1);
        _dispatcherMock.Verify(d => d.DispatchDomainEventsAsync(It.IsAny<CancellationToken>()), Times.Once);
        entryStatesAtDispatch.Should().ContainSingle()
            .Which.Should().Be(EntityState.Added, "domain events must be dispatched before the entities are persisted");
        _context.Entities.AsNoTracking().Count().Should().Be(1);
    }

    // ---------------------------------------------------------------
    // Transactions
    // ---------------------------------------------------------------

    [Fact]
    public async Task CommitTransactionAsync_WhenNoTransactionStarted_ThrowsInvalidOperationException()
    {
        var act = () => _sut.CommitTransactionAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No active transaction. Call BeginTransactionAsync first.");
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenCalled_MarksTransactionActive()
    {
        await _sut.BeginTransactionAsync(CancellationToken.None);

        _sut.HasActiveTransaction.Should().BeTrue();
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenTransactionActive_SavesAndReleasesTransaction()
    {
        _context.Entities.Add(new TestEntity { Id = Guid.NewGuid(), Name = "committed" });
        await _sut.BeginTransactionAsync(CancellationToken.None);

        await _sut.CommitTransactionAsync(CancellationToken.None);

        _dispatcherMock.Verify(d => d.DispatchDomainEventsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _context.Entities.AsNoTracking().Count().Should().Be(1);
        _sut.HasActiveTransaction.Should().BeFalse("commit must release the transaction");
    }

    [Fact]
    public async Task CommitTransactionAsync_WhenSaveFails_RollsBackAndRethrows()
    {
        _context.Entities.Add(new TestEntity { Id = Guid.NewGuid(), Name = "doomed" });
        await _sut.BeginTransactionAsync(CancellationToken.None);
        _dispatcherMock
            .Setup(d => d.DispatchDomainEventsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("handler blew up"));

        var act = () => _sut.CommitTransactionAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>("the original failure must propagate");
        _sut.HasActiveTransaction.Should().BeFalse("a failed commit must release its transaction");
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenTransactionActive_ReleasesTransaction()
    {
        await _sut.BeginTransactionAsync(CancellationToken.None);

        await _sut.RollbackTransactionAsync(CancellationToken.None);

        _sut.HasActiveTransaction.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackTransactionAsync_WhenNoTransaction_IsNoOp()
    {
        var act = () => _sut.RollbackTransactionAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        _sut.HasActiveTransaction.Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_WhenTransactionActive_RollsBackAndWarns()
    {
        var loggerMock = new Mock<ILogger<UnitOfWork<TestDbContext>>>();
        var sut = new UnitOfWork<TestDbContext>(_context, _dispatcherMock.Object, loggerMock.Object);
        await sut.BeginTransactionAsync(CancellationToken.None);

        await sut.DisposeAsync();

        sut.HasActiveTransaction.Should().BeFalse("disposing with an open transaction must roll it back");
        loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString().Contains("disposed with active transaction")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "the rollback on dispose must be visible in the logs");
    }
}
