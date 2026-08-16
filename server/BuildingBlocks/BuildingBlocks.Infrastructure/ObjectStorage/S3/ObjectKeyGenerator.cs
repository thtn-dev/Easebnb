namespace BuildingBlocks.Infrastructure.ObjectStorage.S3;

public static class ObjectKeyGenerator
{
    public static string NewKey(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName);
        return $"{DateTimeOffset.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{ext}";
    }
}