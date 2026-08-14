namespace BuildingBlocks.Application;

public interface IExecutionContextAccessor
{
    CurrentUser User { get; }
    Guid CorrelationId { get; }
    bool IsAvailable { get; }
}