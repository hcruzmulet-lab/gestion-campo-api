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

    public async Task<Dictionary<Guid, TrackingPoint>> GetLastLocationsAsync(
        IEnumerable<Guid> vendorIds, CancellationToken ct = default)
    {
        var ids = vendorIds.ToList();
        var since = DateTime.UtcNow.AddHours(-24);

        var points = await _db.TrackingPoints
            .Where(t => ids.Contains(t.VendorId) && t.CapturedAt >= since)
            .ToListAsync(ct);

        return points
            .GroupBy(t => t.VendorId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(t => t.CapturedAt).First()
            );
    }
}
