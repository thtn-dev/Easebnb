using BuildingBlocks.Infrastructure.DomainEvent;
using BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.UnitTests;

public class DomainEventDispatcherTests
{
    private readonly Mock<IDomainEventsAccessor> _accessorMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly DomainEventDispatcher _sut;

    public DomainEventDispatcherTests()
    {
        _sut = new DomainEventDispatcher(
            _accessorMock.Object,
            _publisherMock.Object,
            NullLogger<DomainEventDispatcher>.Instance);
    }

    private sealed class TestDomainEvent : DomainEventBase;


    // ---------------------------------------------------------------
    // DispatchDomainEventsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task DispatchDomainEventsAsync_WhenNoEvents_DoesNotPublishAnything()
    {
        _accessorMock
            .Setup(a => a.GetAllDomainEvents())
            .Returns([]);

        await _sut.DispatchDomainEventsAsync(CancellationToken.None);

        _publisherMock.Verify(
            p => p.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _accessorMock.Verify(a => a.ClearAllDomainEvents(), Times.Never);
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_WhenEventsExist_PublishesEachEventOnceAndClearsFirst()
    {
        var first = new TestDomainEvent();
        var second = new TestDomainEvent();
        var published = new List<IDomainEvent>();
        // Real accessors return the pending events once and report none after
        // ClearAllDomainEvents; a constant non-empty return would loop forever.
        _accessorMock
            .SetupSequence(a => a.GetAllDomainEvents())
            .Returns([first, second])
            .Returns([]);
        // Tripwire: if the dispatcher ever fails to terminate, fail fast
        // instead of growing `published` without bound.
        _publisherMock
            .Setup(p => p.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) =>
            {
                if (published.Count >= 10)
                    throw new InvalidOperationException(
                        "DispatchDomainEventsAsync did not terminate after the events were cleared");
                published.Add(e);
            })
            .Returns(Task.CompletedTask);

        await _sut.DispatchDomainEventsAsync(CancellationToken.None);

        // Each event is published exactly once.
        published.Should().HaveCount(2);
        published.Count(e => ReferenceEquals(e, first)).Should().Be(1);
        published.Count(e => ReferenceEquals(e, second)).Should().Be(1);
        published.Should().ContainInOrder(first, second);
        // The events were cleared before dispatching.
        _accessorMock.Verify(a => a.ClearAllDomainEvents(), Times.Once);
        // The loop terminated: the accessor was polled again and found empty.
        _accessorMock.Verify(a => a.GetAllDomainEvents(), Times.Exactly(2));
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_WhenHandlersRaiseNewEvents_DispatchesThemBeforeReturning()
    {
        var initial = new TestDomainEvent();
        var raisedByHandler = new TestDomainEvent();
        var published = new List<IDomainEvent>();
        _accessorMock
            .SetupSequence(a => a.GetAllDomainEvents())
            .Returns([initial])
            .Returns([raisedByHandler])
            .Returns([]);
        _publisherMock
            .Setup(p => p.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((e, _) => published.Add(e))
            .Returns(Task.CompletedTask);

        await _sut.DispatchDomainEventsAsync(CancellationToken.None);

        published.Should().HaveCount(2, "the dispatch loop must keep going until no events remain");
        published.Should().ContainInOrder(initial, raisedByHandler);
    }
}
