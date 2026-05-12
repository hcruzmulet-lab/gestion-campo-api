using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface ITrackingRepository
{
    Task AddRangeAsync(IEnumerable<TrackingPoint> points, CancellationToken ct = default);
    Task<List<TrackingPoint>> GetByVendorAndDateRangeAsync(
        Guid vendorId, DateTime from, DateTime to, CancellationToken ct = default);
}
