using Amazon.S3;
using Amazon.S3.Model;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using System.Net;

namespace BuildingBlocks.Infrastructure.ObjectStorage.S3;

public sealed class S3ObjectStorage(IAmazonS3 client) : IObjectStorage
{
    public async Task<PutObjectResult> PutAsync(Application.ObjectStorage.Abstractions.PutObjectRequest request,
        CancellationToken ct)
    {
        try
        {
            var req = new Amazon.S3.Model.PutObjectRequest
            {
                BucketName = request.Bucket,
                Key = request.Key,
                InputStream = request.Content,
                ContentType = request.ContentType,
                AutoCloseStream = false
            };
            if (request.Metadata is not null)
                foreach (var (k, v) in request.Metadata)
                    req.Metadata.Add(k, v);

            var response = await client.PutObjectAsync(req, ct);
            return new PutObjectResult
            {
                Bucket = request.Bucket,
                Key = request.Key,
                ETag = response.ETag,
                VersionId = response.VersionId
            };
        }
        catch (AmazonS3Exception ex)
        {
            throw new ObjectStorageException(ObjectStorageErrorCode.UploadFailed, "S3 put failed", ex);
        }
    }

    public async Task<ObjectStream> GetAsync(
        string bucket,
        string key,
        CancellationToken ct)
    {
        try
        {
            var response = await client.GetObjectAsync(bucket, key, ct);

            return new ObjectStream
            {
                Content = response.ResponseStream,
                Metadata = MapMetadata(bucket, key, response)
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ObjectStorageException(
                ObjectStorageErrorCode.NotFound,
                $"Object {key} not found",
                ex);
        }
    }

    public async Task<ObjectMetadata?> HeadAsync(string bucket, string key, CancellationToken ct)
    {
        try
        {
            var response = await client.GetObjectMetadataAsync(bucket, key, ct);
            return MapMetadata(bucket, key, response);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string bucket, string key, CancellationToken ct)
    {
        return await HeadAsync(bucket, key, ct) is not null;
    }

    public async Task DeleteAsync(string bucket, string key, CancellationToken ct)
    {
        await client.DeleteObjectAsync(bucket, key, ct);
    }

    public async Task<MultipartUploadHandle> InitiateMultipartUploadAsync(
        string bucket, string key, string? contentType, IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct)
    {
        var response = await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = bucket,
            Key = key,
            ContentType = contentType
        }, ct);

        return new MultipartUploadHandle { Bucket = bucket, Key = key, UploadId = response.UploadId };
    }

    public async Task<UploadedPart> UploadPartAsync(Application.ObjectStorage.Abstractions.UploadPartRequest request,
        CancellationToken ct)
    {
        var response = await client.UploadPartAsync(new Amazon.S3.Model.UploadPartRequest
        {
            BucketName = request.Upload.Bucket,
            Key = request.Upload.Key,
            UploadId = request.Upload.UploadId,
            PartNumber = request.PartNumber,
            InputStream = request.Content
        }, ct);

        return new UploadedPart { PartNumber = request.PartNumber, ETag = response.ETag };
    }

    public async Task<PutObjectResult> CompleteMultipartUploadAsync(
        Application.ObjectStorage.Abstractions.CompleteMultipartUploadRequest request, CancellationToken ct)
    {
        var response = await client.CompleteMultipartUploadAsync(new Amazon.S3.Model.CompleteMultipartUploadRequest
        {
            BucketName = request.Upload.Bucket,
            Key = request.Upload.Key,
            UploadId = request.Upload.UploadId,
            PartETags = [.. request.Parts.Select(p => new PartETag(p.PartNumber, p.ETag))]
        }, ct);

        return new PutObjectResult { Bucket = request.Upload.Bucket, Key = request.Upload.Key, ETag = response.ETag };
    }

    public Task AbortMultipartUploadAsync(MultipartUploadHandle upload, CancellationToken ct)
    {
        return client.AbortMultipartUploadAsync(upload.Bucket, upload.Key, upload.UploadId, ct);
    }

    private static ObjectMetadata MapMetadata(
        string bucket,
        string key,
        GetObjectResponse r)
    {
        return new ObjectMetadata
        {
            Bucket = bucket,
            Key = key,
            ContentType = r.Headers.ContentType,
            ContentLength = r.Headers.ContentLength,
            ETag = r.ETag,
            VersionId = r.VersionId,
            LastModified = r.LastModified,
            UserMetadata = r.Metadata.Keys.ToDictionary(
                k => k,
                k => r.Metadata[k])
        };
    }

    private static ObjectMetadata MapMetadata(string bucket, string key, GetObjectMetadataResponse r)
    {
        return new ObjectMetadata
        {
            Bucket = bucket,
            Key = key,
            ContentType = r.Headers.ContentType,
            ContentLength = r.Headers.ContentLength,
            ETag = r.ETag,
            VersionId = r.VersionId,
            LastModified = r.LastModified,
            UserMetadata = r.Metadata.Keys.ToDictionary(k => k, k => r.Metadata[k])
        };
    }
}