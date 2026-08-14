using MediatR;

namespace BuildingBlocks.SharedKernel;

public interface IPostCommitEvent : INotification
{
    Guid Id { get; }
    DateTimeOffset OccurredOn { get; }
}