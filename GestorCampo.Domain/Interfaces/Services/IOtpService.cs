namespace GestorCampo.Domain.Interfaces.Services;

public interface IOtpService
{
    string GenerateCode();
    Task StoreAsync(Guid userId, string code, string purpose, int expiryMinutes, CancellationToken ct = default);
    Task<bool> ValidateAsync(Guid userId, string code, string purpose, CancellationToken ct = default);
}
