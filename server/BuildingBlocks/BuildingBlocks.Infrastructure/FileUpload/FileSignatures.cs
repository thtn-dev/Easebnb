namespace BuildingBlocks.Infrastructure.FileUpload;

/// <summary>
/// File signatures (magic bytes) for common file formats, used to validate the actual
/// file content instead of trusting the extension/Content-Type provided by the client.
/// </summary>
public static class FileSignatures
{
    public static readonly IReadOnlyDictionary<string, byte[][]> Map = new Dictionary<string, byte[][]>
    {
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".gif"] = [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61], [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]],
        [".webp"] = [[0x52, 0x49, 0x46, 0x46]], // + separate "WEBP" check at offset 8
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]]
    };

    /// <summary>Common image extensions typically used for avatars/profile pictures.</summary>
    public static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];
}