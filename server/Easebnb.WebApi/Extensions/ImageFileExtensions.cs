using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Easebnb.WebApi.Extensions;

/// <summary>
/// Image processing extensions to be used after <see cref="FormFileExtensions.ValidateAsync"/>.
/// Re-encodes images to remove any embedded malicious payload/metadata from the original file
/// (EXIF exploits, polyglot files, etc.) and normalizes the image size.
/// </summary>
public static class ImageFileExtensions
{
    /// <summary>
    /// Decodes an image from an IFormFile, resizes it based on its longest edge,
    /// and re-encodes it as JPEG.
    /// Returns null if the file cannot be decoded as a valid image, even if it
    /// has passed the signature check.
    /// The returned stream has its Position set to 0; the caller is responsible for disposing it.
    /// </summary>
    public static async Task<MemoryStream?> ToSafeJpegAsync(
        this IFormFile file,
        int maxDimension = 512,
        int quality = 85,
        CancellationToken ct = default)
    {
        await using var input = file.OpenReadStream();

        Image image;
        try
        {
            image = await Image.LoadAsync(input, ct);
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }

        using (image)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max
            }));

            var output = new MemoryStream();
            await image.SaveAsync(output, new JpegEncoder { Quality = quality }, ct);
            output.Position = 0;
            return output;
        }
    }
}