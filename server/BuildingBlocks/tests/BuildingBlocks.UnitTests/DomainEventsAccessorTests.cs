using BuildingBlocks.Infrastructure.DomainEvent;
using BuildingBlocks.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.UnitTests;

public class DomainEventsAccessorTests : IDisposable
{
    private readonly EventDbContext _context = CreateContext();

    public sealed class DomainEntity : IHasDomainEvents
    {
        private readonly List<IDomainEvent> _events = [];

        public Guid Id { get; set; }

        public IReadOnlyCollection<IDomainEvent>? DomainEvents => _events.AsReadOnly();

        public void ClearDomainEvents() => _events.Clear();

        public void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);
    }

    public sealed class EventDbContext(DbContextOptions<EventDbContext> options) : DbContext(options)
    {
        public DbSet<DomainEntity> Entities => Set<DomainEntity>();
    }

    private static EventDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new EventDbContext(options);
    }

    private sealed class TestDomainEvent : DomainEventBase;

    public void Dispose() => _context.Dispose();


    // ---------------------------------------------------------------
    // GetAllDomainEvents
    // ---------------------------------------------------------------

    [Fact]
    public void GetAllDomainEvents_WhenEntitiesHaveEvents_ReturnsAllTrackedEvents()
    {
        var first = new DomainEntity();
        var second = new DomainEntity();
        var firstEvent = new TestDomainEvent();
        var secondEvent = new TestDomainEvent();
        first.Raise(firstEvent);
        second.Raise(secondEvent);
        _context.Entities.AddRange(first, second);
        var accessor = new DomainEventsAccessor<EventDbContext>(_context);

        var events = accessor.GetAllDomainEvents();

        events.Should().HaveCount(2);
        events.Should().Contain(firstEvent);
        events.Should().Contain(secondEvent);
    }

    [Fact]
    public void GetAllDomainEvents_WhenNoEntityHasEvents_ReturnsEmpty()
    {
        _context.Entities.Add(new DomainEntity());
        var accessor = new DomainEventsAccessor<EventDbContext>(_context);

        var events = accessor.GetAllDomainEvents();

        events.Should().BeEmpty();
    }

    // ---------------------------------------------------------------
    // ClearAllDomainEvents
    // ---------------------------------------------------------------

    [Fact]
    public void ClearAllDomainEvents_WhenCalled_EmptiesEveryTrackedEntity()
    {
        var entity = new DomainEntity();
        entity.Raise(new TestDomainEvent());
        _context.Entities.Add(entity);
        var accessor = new DomainEventsAccessor<EventDbContext>(_context);

        accessor.ClearAllDomainEvents();

        entity.DomainEvents.Should().BeEmpty("cleared entities must not dispatch stale events on the next save");
    }
}
