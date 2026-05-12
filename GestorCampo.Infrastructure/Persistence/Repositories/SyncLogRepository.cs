using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class SyncLogRepository : ISyncLogRepository
{
    private readonly AppDbContext _db;

    public SyncLogRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SyncLog log, CancellationToken ct = default)
    {
        await _db.SyncLogs.AddAsync(log, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SyncLog log, CancellationToken ct = default)
    {
        _db.SyncLogs.Update(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(List<SyncLog> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        string? adapter, string? status,
        DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var query = _db.SyncLogs.AsQueryable();

        if (!string.IsNullOrEmpty(adapter))
            query = query.Where(s => s.Adapter == adapter);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(s => s.Status == status);
        if (from.HasValue)
            query = query.Where(s => s.StartedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(s => s.StartedAt <= to.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
