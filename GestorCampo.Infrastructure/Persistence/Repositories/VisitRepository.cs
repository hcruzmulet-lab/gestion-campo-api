using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class VisitRepository : IVisitRepository
{
    private readonly AppDbContext _db;

    public VisitRepository(AppDbContext db) => _db = db;

    public Task<Visit?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Visits.FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<(List<Visit> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        VisitStatus? status, Guid? vendorId, Guid? clientId,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _db.Visits.AsQueryable();

        if (status.HasValue)
            query = query.Where(v => v.Status == status.Value);
        if (vendorId.HasValue)
            query = query.Where(v => v.VendorId == vendorId.Value);
        if (clientId.HasValue)
            query = query.Where(v => v.ClientId == clientId.Value);
        if (from.HasValue)
            query = query.Where(v => v.PlannedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(v => v.PlannedAt <= to.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(v => v.PlannedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Visit visit, CancellationToken ct = default)
    {
        await _db.Visits.AddAsync(visit, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Visit visit, CancellationToken ct = default)
    {
        _db.Visits.Update(visit);
        await _db.SaveChangesAsync(ct);
    }
}
