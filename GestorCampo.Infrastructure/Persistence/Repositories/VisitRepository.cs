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
        _db.Visits
            .Include(v => v.Client)
            .Include(v => v.Vendor)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<(List<Visit> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        VisitStatus? status, Guid? vendorId, Guid? clientId,
        DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _db.Visits
            .Include(v => v.Client)
            .Include(v => v.Vendor)
            .AsQueryable();

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

    public Task<bool> HasInProgressForVendorAsync(Guid vendorId, CancellationToken ct = default) =>
        _db.Visits.AnyAsync(
            v => v.VendorId == vendorId && v.Status == VisitStatus.InProgress,
            ct);

    public async Task<Dictionary<Guid, DateTime>> GetLastCheckinByVendorAsync(
        IEnumerable<Guid> vendorIds, CancellationToken ct = default)
    {
        var ids = vendorIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime>();
        var rows = await _db.Visits
            .Where(v => ids.Contains(v.VendorId) && v.CheckinAt.HasValue)
            .GroupBy(v => v.VendorId)
            .Select(g => new { VendorId = g.Key, Last = g.Max(v => v.CheckinAt!.Value) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.VendorId, r => r.Last);
    }
}
