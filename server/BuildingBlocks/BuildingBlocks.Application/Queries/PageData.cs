namespace BuildingBlocks.Application.Queries;

public readonly struct PageData(int offset, int next)
{
    public int Offset { get; } = offset;

    public int Next { get; } = next;
}