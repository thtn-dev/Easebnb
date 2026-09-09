
using Amazon.S3;
using AwsModels = Amazon.S3.Model;
using BuildingBlocks.Application.ObjectStorage.Abstractions;
using BuildingBlocks.Infrastructure.ObjectStorage.S3;

namespace BuildingBlocks.UnitTests;

public class S3ObjectStorageTests
{
    private readonly Mock<IAmazonS3> _clientMock = new();
    private readonly S3ObjectStorage _sut;

    public S3ObjectStorageTests()
    {
        _sut = new S3ObjectStorage(_clientMock.Object);
    }

    private static PutObjectRequest CreatePutRequest(
        string bucket = "easebnb-users",
        string key = "2026/08/22/abc.png",
        Dictionary<string, string>? metadata = null) =>
        new()
        {
            Bucket = bucket,
            Key = key,
            Content = new MemoryStream([1, 2, 3]),
            ContentType = "image/png",
            Metadata = metadata
        };

    private static AmazonS3Exception CreateS3Exception(System.Net.HttpStatusCode statusCode) =>
        new("s3 error") { StatusCode = statusCode };


    // ---------------------------------------------------------------
    // PutAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task PutAsync_WhenSucceeds_ReturnsResultAndSendsMetadata()
    {
        var request = CreatePutRequest(metadata: new Dictionary<string, string> { ["file-name"] = "avatar.png" });
        AwsModels.PutObjectRequest? captured = null;
        _clientMock
            .Setup(c => c.PutObjectAsync(It.IsAny<AwsModels.PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AwsModels.PutObjectRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new AwsModels.PutObjectResponse { ETag = "\"etag1\"", VersionId = "v1" });

        var result = await _sut.PutAsync(request, CancellationToken.None);

        result.Bucket.Should().Be("easebnb-users");
        result.Key.Should().Be("2026/08/22/abc.png");
        result.ETag.Should().Be("\"etag1\"");
        result.VersionId.Should().Be("v1");
        captured.Should().NotBeNull();
        captured!.BucketName.Should().Be("easebnb-users");
        captured.Key.Should().Be("2026/08/22/abc.png");
        captured.ContentType.Should().Be("image/png");
        captured.Metadata["file-name"].Should().Be("avatar.png", "custom metadata must be forwarded to S3");
    }

    [Fact]
    public async Task PutAsync_WhenS3Throws_ThrowsObjectStorageExceptionUploadFailed()
    {
        _clientMock
            .Setup(c => c.PutObjectAsync(It.IsAny<AwsModels.PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("bucket missing"));

        var act = () => _sut.PutAsync(CreatePutRequest(), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ObjectStorageException>();
        exception.Which.ErrorCode.Should().Be(ObjectStorageErrorCode.UploadFailed);
        exception.Which.InnerException.Should().BeOfType<AmazonS3Exception>();
    }

    // ---------------------------------------------------------------
    // GetAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAsync_WhenObjectExists_ReturnsStreamWithMetadata()
    {
        var content = new MemoryStream([1, 2, 3]);
        var response = new AwsModels.GetObjectResponse
        {
            ResponseStream = content,
            ETag = "\"etag2\"",
            VersionId = "v2"
        };
        response.Headers.ContentType = "image/png";
        response.Headers.ContentLength = 3;
        _clientMock
            .Setup(c => c.GetObjectAsync("easebnb-users", "2026/08/22/abc.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await using var stream = await _sut.GetAsync("easebnb-users", "2026/08/22/abc.png", CancellationToken.None);

        stream.Content.Should().BeSameAs(content);
        stream.Metadata.Bucket.Should().Be("easebnb-users");
        stream.Metadata.Key.Should().Be("2026/08/22/abc.png");
        stream.Metadata.ETag.Should().Be("\"etag2\"");
        stream.Metadata.ContentType.Should().Be("image/png");
        stream.Metadata.ContentLength.Should().Be(3);
    }

    [Fact]
    public async Task GetAsync_WhenObjectNotFound_ThrowsObjectStorageExceptionNotFound()
    {
        _clientMock
            .Setup(c => c.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateS3Exception(System.Net.HttpStatusCode.NotFound));

        var act = () => _sut.GetAsync("easebnb-users", "missing", CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ObjectStorageException>();
        exception.Which.ErrorCode.Should().Be(ObjectStorageErrorCode.NotFound);
        exception.Which.Message.Should().Contain("missing");
    }

    [Fact]
    public async Task GetAsync_WhenS3ErrorIsNotNotFound_PropagatesAmazonException()
    {
        _clientMock
            .Setup(c => c.GetObjectAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateS3Exception(System.Net.HttpStatusCode.Forbidden));

        var act = () => _sut.GetAsync("easebnb-users", "denied", CancellationToken.None);

        await act.Should().ThrowAsync<AmazonS3Exception>(
            "only NotFound is mapped; other S3 failures must surface as-is");
    }

    // ---------------------------------------------------------------
    // HeadAsync / ExistsAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task HeadAsync_WhenObjectExists_ReturnsMetadata()
    {
        var response = new AwsModels.GetObjectMetadataResponse { ETag = "\"etag3\"" };
        response.Headers.ContentType = "application/pdf";
        response.Headers.ContentLength = 42;
        _clientMock
            .Setup(c => c.GetObjectMetadataAsync("docs", "report.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var metadata = await _sut.HeadAsync("docs", "report.pdf", CancellationToken.None);

        metadata.Should().NotBeNull();
        metadata!.Bucket.Should().Be("docs");
        metadata.Key.Should().Be("report.pdf");
        metadata.ETag.Should().Be("\"etag3\"");
        metadata.ContentType.Should().Be("application/pdf");
        metadata.ContentLength.Should().Be(42);
    }

    [Fact]
    public async Task HeadAsync_WhenObjectNotFound_ReturnsNull()
    {
        _clientMock
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateS3Exception(System.Net.HttpStatusCode.NotFound));

        var metadata = await _sut.HeadAsync("docs", "missing.pdf", CancellationToken.None);

        metadata.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_WhenHeadReturnsMetadata_ReturnsTrue()
    {
        _clientMock
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwsModels.GetObjectMetadataResponse());

        var exists = await _sut.ExistsAsync("docs", "report.pdf", CancellationToken.None);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenHeadReturnsNull_ReturnsFalse()
    {
        _clientMock
            .Setup(c => c.GetObjectMetadataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CreateS3Exception(System.Net.HttpStatusCode.NotFound));

        var exists = await _sut.ExistsAsync("docs", "missing.pdf", CancellationToken.None);

        exists.Should().BeFalse();
    }

    // ---------------------------------------------------------------
    // DeleteAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_WhenCalled_DeletesFromBucketAndKey()
    {
        await _sut.DeleteAsync("easebnb-users", "2026/08/22/abc.png", CancellationToken.None);

        _clientMock.Verify(
            c => c.DeleteObjectAsync("easebnb-users", "2026/08/22/abc.png", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Multipart upload
    // ---------------------------------------------------------------

    [Fact]
    public async Task InitiateMultipartUploadAsync_WhenSucceeds_ReturnsHandleWithUploadId()
    {
        _clientMock
            .Setup(c => c.InitiateMultipartUploadAsync(It.IsAny<AwsModels.InitiateMultipartUploadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwsModels.InitiateMultipartUploadResponse { UploadId = "upload-1" });

        var handle = await _sut.InitiateMultipartUploadAsync("videos", "large.mp4", "video/mp4", null, CancellationToken.None);

        handle.Bucket.Should().Be("videos");
        handle.Key.Should().Be("large.mp4");
        handle.UploadId.Should().Be("upload-1");
    }

    [Fact]
    public async Task UploadPartAsync_WhenSucceeds_ReturnsPartNumberAndEtag()
    {
        var upload = new MultipartUploadHandle { Bucket = "videos", Key = "large.mp4", UploadId = "upload-1" };
        var request = new UploadPartRequest { Upload = upload, PartNumber = 1, Content = new MemoryStream([1]) };
        _clientMock
            .Setup(c => c.UploadPartAsync(It.IsAny<AwsModels.UploadPartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AwsModels.UploadPartResponse { ETag = "\"part1\"" });

        var part = await _sut.UploadPartAsync(request, CancellationToken.None);

        part.PartNumber.Should().Be(1);
        part.ETag.Should().Be("\"part1\"");
    }

    [Fact]
    public async Task CompleteMultipartUploadAsync_WhenSucceeds_CombinesPartEtags()
    {
        var upload = new MultipartUploadHandle { Bucket = "videos", Key = "large.mp4", UploadId = "upload-1" };
        var request = new CompleteMultipartUploadRequest
        {
            Upload = upload,
            Parts =
            [
                new UploadedPart { PartNumber = 1, ETag = "\"part1\"" },
                new UploadedPart { PartNumber = 2, ETag = "\"part2\"" }
            ]
        };
        AwsModels.CompleteMultipartUploadRequest? captured = null;
        _clientMock
            .Setup(c => c.CompleteMultipartUploadAsync(It.IsAny<AwsModels.CompleteMultipartUploadRequest>(), It.IsAny<CancellationToken>()))
            .Callback<AwsModels.CompleteMultipartUploadRequest, CancellationToken>((req, _) => captured = req)
            .ReturnsAsync(new AwsModels.CompleteMultipartUploadResponse { ETag = "\"final\"" });

        var result = await _sut.CompleteMultipartUploadAsync(request, CancellationToken.None);

        result.ETag.Should().Be("\"final\"");
        result.Bucket.Should().Be("videos");
        result.Key.Should().Be("large.mp4");
        captured.Should().NotBeNull();
        captured!.PartETags.Should().HaveCount(2);
        captured.PartETags[0].PartNumber.Should().Be(1);
        captured.PartETags[0].ETag.Should().Be("\"part1\"");
        captured.PartETags[1].PartNumber.Should().Be(2);
        captured.PartETags[1].ETag.Should().Be("\"part2\"");
    }

    [Fact]
    public async Task AbortMultipartUploadAsync_WhenCalled_AbortsSession()
    {
        var upload = new MultipartUploadHandle { Bucket = "videos", Key = "large.mp4", UploadId = "upload-1" };

        await _sut.AbortMultipartUploadAsync(upload, CancellationToken.None);

        _clientMock.Verify(
            c => c.AbortMultipartUploadAsync("videos", "large.mp4", "upload-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
