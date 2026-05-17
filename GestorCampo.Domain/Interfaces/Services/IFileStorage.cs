namespace GestorCampo.Domain.Interfaces.Services;

public interface IFileStorage
{
    Task<string> UploadAsync(Stream content, string key, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);
    Task<string> GetPresignedReadUrlAsync(string key, TimeSpan ttl, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
