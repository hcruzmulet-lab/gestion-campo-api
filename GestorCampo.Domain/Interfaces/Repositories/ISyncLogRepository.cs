using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface ISyncLogRepository
{
    Task AddAsync(SyncLog log, CancellationToken ct = default);
    Task UpdateAsync(SyncLog log, CancellationToken ct = default);
    Task<(List<SyncLog> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        string? adapter, string? status,
        DateTime? from, DateTime? to,
        CancellationToken ct = default);
}
