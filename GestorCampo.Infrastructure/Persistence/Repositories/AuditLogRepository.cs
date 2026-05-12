using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _db;

    public AuditLogRepository(AppDbContext db) => _db = db;

    public async Task<(List<AuditLog> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        Guid? userId, string? module,
        DateTime? from, DateTime? to,
        CancellationToken ct = default)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (userId.HasValue)
            query = query.Where(l => l.UserId == userId.Value);
        if (!string.IsNullOrEmpty(module))
            query = query.Where(l => l.Module == module);
        if (from.HasValue)
            query = query.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(l => l.CreatedAt <= to.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }
}
