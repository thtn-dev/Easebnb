using BuildingBlocks.Infrastructure.FileUpload;
using Microsoft.AspNetCore.Http.Features;

namespace Easebnb.WebApi.Extensions;

public readonly record struct FileValidationResult(bool IsValid, string? Error)
{
    public static FileValidationResult Ok()
    {
        return new FileValidationResult(true, null);
    }

    public static FileValidationResult Fail(string error)
    {
        return new FileValidationResult(false, error);
    }
}

/// <summary>
/// Security extension methods for <see cref="IFormFile"/>.
/// Use individual methods separately or call <see cref="ValidateAsync"/> to run the full validation pipeline.
/// </summary>
public static class FormFileExtensions
{
    extension(IFormFile file)
    {
        /// <summary>Checks that the file size does not exceed the allowed limit.</summary>
        public FileValidationResult ValidateSize(long maxBytes)
        {
            if (file.Length == 0)
                return FileValidationResult.Fail("File is empty.");

            if (file.Length > maxBytes)
                return FileValidationResult.Fail($"File exceeds the allowed limit of {maxBytes / 1024 / 1024}MB.");

            return FileValidationResult.Ok();
        }

        /// <summary>Checks that the extension is in the whitelist (case-insensitive).</summary>
        public FileValidationResult ValidateExtension(IEnumerable<string> permittedExtensions)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
                return FileValidationResult.Fail("File format is not supported.");

            return FileValidationResult.Ok();
        }

        /// <summary>
        /// Verifies that the file's magic bytes match the declared extension.
        /// Must be used after <see cref="ValidateExtension"/> — do not trust the extension alone.
        /// </summary>
        public async Task<FileValidationResult> ValidateSignatureAsync(CancellationToken ct = default)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!FileSignatures.Map.TryGetValue(ext, out var signatures))
                return FileValidationResult.Fail("No reference signature is available for this format.");

            await using var stream = file.OpenReadStream();
            var maxLen = signatures.Max(s => s.Length);
            var header = new byte[maxLen];
            var read = await stream.ReadAsync(header.AsMemory(0, maxLen), ct);

            if (read < maxLen || !signatures.Any(sig => header.AsSpan(0, sig.Length).SequenceEqual(sig)))
                return FileValidationResult.Fail("File content does not match the declared format.");

            // WEBP shares the RIFF header with AVI/WAV -> additionally check for the "WEBP" marker at offset 8
            if (ext == ".webp")
            {
                stream.Position = 8;
                var marker = new byte[4];
                await stream.ReadExactlyAsync(marker.AsMemory(0, 4), ct);
                if (!marker.AsSpan().SequenceEqual("WEBP"u8))
                    return FileValidationResult.Fail("File content does not match the WEBP format.");
            }

            return FileValidationResult.Ok();
        }

        /// <summary>
        /// Runs the full validation pipeline: size -> extension -> signature.
        /// Fails fast on the first failed step.
        /// </summary>
        public async Task<FileValidationResult> ValidateAsync(long maxBytes,
            IEnumerable<string> permittedExtensions,
            CancellationToken ct = default)
        {
            var sizeResult = file.ValidateSize(maxBytes);
            if (!sizeResult.IsValid) return sizeResult;

            var extResult = file.ValidateExtension(permittedExtensions);
            if (!extResult.IsValid) return extResult;

            return await file.ValidateSignatureAsync(ct);
        }

        /// <summary>
        /// Generates a safe random file name while preserving the original extension.
        /// NEVER use <see cref="IFormFile.FileName"/> directly to store files
        /// (risk of path traversal or file overwrite).
        /// </summary>
        public string GenerateSafeFileName(string? prefix = null)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var name = $"{Guid.NewGuid():N}{ext}";
            return string.IsNullOrEmpty(prefix) ? name : $"{prefix}_{name}";
        }
    }
}

/// <summary>
/// Request-level helper extensions to use before handling IFormFile instances.
/// </summary>
public static class HttpRequestExtensions
{
    /// <summary>
    /// Sets a per-endpoint request body size limit (overrides server-wide Kestrel/IIS limits configured in Program.cs). Call at the start of the handler, before reading the form/body.
    /// </summary>
    public static void SetMaxBodySize(this HttpContext context, long maxBytes)
    {
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = maxBytes;
    }

    /// <summary>Checks whether the request is multipart/form-data (early rejection for incorrect content type).</summary>
    public static bool IsMultipartFormData(this HttpRequest request)
    {
        return !string.IsNullOrEmpty(request.ContentType)
               && request.ContentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase);
    }
}