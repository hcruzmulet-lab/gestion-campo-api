using Amazon.S3;
using Amazon.S3.Model;
using GestorCampo.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace GestorCampo.Infrastructure.Services.Storage;

public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _client;
    private readonly S3StorageOptions _opts;

    public S3FileStorage(IAmazonS3 client, IOptions<S3StorageOptions> opts)
    {
        _client = client;
        _opts = opts.Value;
    }

    public async Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default)
    {
        var put = new PutObjectRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            // Cloudflare R2 does not implement aws-chunked streaming
            // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD) nor the AWS SDK's default
            // request checksums. Disable both so the PUT uses an UNSIGNED
            // single-shot payload that R2 accepts. Harmless against MinIO/S3.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
            UseChunkEncoding = false
        };
        await _client.PutObjectAsync(put, ct);
        return key;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var resp = await _client.GetObjectAsync(_opts.Bucket, key, ct);
        return resp.ResponseStream;
    }

    public Task<string> GetPresignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default)
    {
        var protocol = _opts.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTP
            : Protocol.HTTPS;
        var url = _client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _opts.Bucket,
            Key = key,
            Expires = DateTime.UtcNow.Add(ttl),
            Verb = HttpVerb.GET,
            Protocol = protocol
        });
        return Task.FromResult(url);
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await _client.DeleteObjectAsync(_opts.Bucket, key, ct);
    }
}
