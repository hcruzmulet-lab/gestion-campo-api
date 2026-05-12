using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class TrackingRepository : ITrackingRepository
{
    private readonly AppDbContext _db;

    public TrackingRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<TrackingPoint> points, CancellationToken ct = default)
    {
        await _db.TrackingPoints.AddRangeAsync(points, ct);
        await _db.SaveChangesAsync(ct);
    }

    public Task<List<TrackingPoint>> GetByVendorAndDateRangeAsync(
        Guid vendorId, DateTime from, DateTime to, CancellationToken ct = default) =>
        _db.TrackingPoints
            .Where(t => t.VendorId == vendorId && t.CapturedAt >= from && t.CapturedAt <= to)
            .OrderBy(t => t.CapturedAt)
            .ToListAsync(ct);
}
