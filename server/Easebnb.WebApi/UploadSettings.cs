namespace Easebnb.WebApi;

public sealed class UploadSettings
{
    public long GlobalMaxBodySizeBytes { get; set; } = 30 * 1024 * 1024; // 30MB
}

public sealed class AvatarUploadSettings
{
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5MB
    public int MaxDimension { get; set; } = 512;
}