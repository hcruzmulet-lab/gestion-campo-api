using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IEnumerable<RefreshToken>> GetActiveByFamilyAsync(string family, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllByFamilyAsync(string family, CancellationToken ct = default);
    Task RevokeAsync(RefreshToken token, CancellationToken ct = default);
}
