namespace BuildingBlocks.Infrastructure.ObjectStorage.S3;

public sealed class S3StorageOptions
{
    public required string Endpoint { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string Region { get; init; }
    public bool ForcePathStyle { get; init; } = false;
}
