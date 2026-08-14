namespace BuildingBlocks.Application.ObjectStorage.Abstractions;

/// <summary>
/// Error codes for object storage operations.
/// </summary>
public enum ObjectStorageErrorCode
{
    /// <summary>Object not found</summary>
    NotFound,

    /// <summary>Single object upload failed</summary>
    UploadFailed,

    /// <summary>Multipart upload failed</summary>
    MultipartUploadFailed,

    /// <summary>Invalid or corrupted part in multipart upload</summary>
    InvalidPart,

    /// <summary>Storage provider is unavailable</summary>
    ProviderUnavailable
}

/// <summary>
/// Exception thrown by object storage operations.
/// </summary>
public sealed class ObjectStorageException : Exception
{
    /// <summary>Gets the error code for this exception</summary>
    public ObjectStorageErrorCode ErrorCode { get; }

    /// <summary>
    /// Creates a new object storage exception.
    /// </summary>
    /// <param name="errorCode">The error code</param>
    /// <param name="message">Error message</param>
    /// <param name="inner">Inner exception, if any</param>
    public ObjectStorageException(ObjectStorageErrorCode errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }
}