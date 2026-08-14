namespace BuildingBlocks.Application.ObjectStorage.Abstractions;

/// <summary>
/// Metadata describing an existing object in storage (returned by Head/Get operations).
/// </summary>
public sealed class ObjectMetadata
{
    /// <summary>Gets the bucket name</summary>
    public required string Bucket { get; init; }

    /// <summary>Gets the object key</summary>
    public required string Key { get; init; }

    /// <summary>Gets the MIME type, if available</summary>
    public string? ContentType { get; init; }

    /// <summary>Gets the object size in bytes, if available</summary>
    public long? ContentLength { get; init; }

    /// <summary>Gets the ETag (opaque content hash), if available</summary>
    public string? ETag { get; init; }

    /// <summary>Gets the version ID for versioned storage, if available</summary>
    public string? VersionId { get; init; }

    /// <summary>Gets the last modification time, if available</summary>
    public DateTimeOffset? LastModified { get; init; }

    /// <summary>Gets custom metadata attached to the object</summary>
    public IReadOnlyDictionary<string, string>? UserMetadata { get; init; }
}

/// <summary>
/// Request for uploading an object (single PUT operation).
/// </summary>
public sealed class PutObjectRequest
{
    /// <summary>Gets the destination bucket name</summary>
    public required string Bucket { get; init; }

    /// <summary>Gets the object key</summary>
    public required string Key { get; init; }

    /// <summary>Gets the content stream to upload</summary>
    public required Stream Content { get; init; }

    /// <summary>Gets the MIME type, if known</summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Gets the content length. Providing this avoids buffering to calculate length.
    /// </summary>
    public long? ContentLength { get; init; }

    /// <summary>Gets optional custom metadata to attach to the object</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// Result of a successful upload operation.
/// </summary>
public sealed class PutObjectResult
{
    /// <summary>Gets the bucket name</summary>
    public required string Bucket { get; init; }

    /// <summary>Gets the object key</summary>
    public required string Key { get; init; }

    /// <summary>Gets the ETag of the uploaded object</summary>
    public string? ETag { get; init; }

    /// <summary>Gets the version ID if versioning is enabled</summary>
    public string? VersionId { get; init; }
}

/// <summary>
/// Wrapper combining a content stream with its metadata.
/// Must be used with <c>await using</c> to ensure proper disposal.
/// </summary>
public sealed class ObjectStream : IAsyncDisposable
{
    /// <summary>Gets the content stream</summary>
    public required Stream Content { get; init; }

    /// <summary>Gets the object metadata</summary>
    public required ObjectMetadata Metadata { get; init; }

    /// <summary>
    /// Disposes the content stream asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Content is IAsyncDisposable ad)
            await ad.DisposeAsync();
        else
            Content.Dispose();
    }
}

/// <summary>
/// Handle representing an active multipart upload session.
/// The UploadId is opaque and should not be parsed by application code.
/// </summary>
public sealed class MultipartUploadHandle
{
    /// <summary>Gets the bucket name</summary>
    public required string Bucket { get; init; }

    /// <summary>Gets the object key</summary>
    public required string Key { get; init; }

    /// <summary>Gets the opaque upload session ID</summary>
    public required string UploadId { get; init; }
}

/// <summary>
/// Request for uploading a single part in a multipart upload.
/// </summary>
public sealed class UploadPartRequest
{
    /// <summary>Gets the multipart upload handle</summary>
    public required MultipartUploadHandle Upload { get; init; }

    /// <summary>
    /// Gets the part number (1-based, as per multipart storage convention).
    /// Must be between 1 and the maximum allowed by the storage provider.
    /// </summary>
    public required int PartNumber { get; init; }

    /// <summary>Gets the part content stream</summary>
    public required Stream Content { get; init; }

    /// <summary>Gets the part size in bytes (optional, but recommended)</summary>
    public long? ContentLength { get; init; }
}

/// <summary>
/// Metadata of an uploaded part for use in multipart completion.
/// The ETag is opaque and required for the final completion step.
/// </summary>
public sealed class UploadedPart
{
    /// <summary>Gets the part number (1-based)</summary>
    public required int PartNumber { get; init; }

    /// <summary>Gets the opaque ETag identifier for this part</summary>
    public required string ETag { get; init; }
}

/// <summary>
/// Request to complete a multipart upload by combining all parts.
/// </summary>
public sealed class CompleteMultipartUploadRequest
{
    /// <summary>Gets the multipart upload handle</summary>
    public required MultipartUploadHandle Upload { get; init; }

    /// <summary>
    /// Gets all uploaded parts (must be in part number order).
    /// </summary>
    public required IReadOnlyList<UploadedPart> Parts { get; init; }
}