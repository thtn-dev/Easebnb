namespace BuildingBlocks.Application.ObjectStorage.Abstractions;

/// <summary>
/// Abstraction for object storage operations (e.g., S3, Azure Blob, MinIO).
/// Supports single uploads and multipart streaming for large files.
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Upload an object (single PUT operation).
    /// </summary>
    /// <param name="request">Upload request with bucket, key, content, and optional metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Upload result with ETag and VersionId</returns>
    Task<PutObjectResult> PutAsync(PutObjectRequest request, CancellationToken ct = default);

    /// <summary>
    /// Download an object as a stream with metadata.
    /// Must be used with <c>await using</c> to ensure proper resource cleanup.
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="key">Object key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Stream with metadata, or null if not found</returns>
    Task<ObjectStream> GetAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Retrieve metadata of an object without downloading content.
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="key">Object key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Metadata if object exists, null otherwise</returns>
    Task<ObjectMetadata?> HeadAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Check if an object exists.
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="key">Object key</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if object exists, false otherwise</returns>
    Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Delete an object.
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="key">Object key</param>
    /// <param name="ct">Cancellation token</param>
    Task DeleteAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>
    /// Initiate a multipart upload session for streaming large files.
    /// </summary>
    /// <param name="bucket">Bucket name</param>
    /// <param name="key">Object key</param>
    /// <param name="contentType">Optional MIME type</param>
    /// <param name="metadata">Optional custom metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Handle to use for uploading parts and completion</returns>
    Task<MultipartUploadHandle> InitiateMultipartUploadAsync(
        string bucket,
        string key,
        string? contentType = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Upload a part in a multipart upload session.
    /// </summary>
    /// <param name="request">Part upload request with handle, part number, and content</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Part metadata (PartNumber and ETag for later completion)</returns>
    Task<UploadedPart> UploadPartAsync(UploadPartRequest request, CancellationToken ct = default);

    /// <summary>
    /// Complete a multipart upload by combining all uploaded parts.
    /// </summary>
    /// <param name="request">Completion request with upload handle and all parts</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Final result with ETag and VersionId</returns>
    Task<PutObjectResult> CompleteMultipartUploadAsync(
        CompleteMultipartUploadRequest request, CancellationToken ct = default);

    /// <summary>
    /// Abort an ongoing multipart upload session, freeing storage resources.
    /// </summary>
    /// <param name="upload">Upload handle to abort</param>
    /// <param name="ct">Cancellation token</param>
    Task AbortMultipartUploadAsync(MultipartUploadHandle upload, CancellationToken ct = default);
}