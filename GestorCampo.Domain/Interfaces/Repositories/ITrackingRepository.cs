using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface ITrackingRepository
{
    Task AddRangeAsync(IEnumerable<TrackingPoint> points, CancellationToken ct = default);
    Task<List<TrackingPoint>> GetByVendorAndDateRangeAsync(
        Guid vendorId, DateTime from, DateTime to, CancellationToken ct = default);
    /// <summary>
    /// Returns the most recent TrackingPoint in the last 24 hours per vendor.
    /// Vendors with no recent point are absent from the returned dictionary.
    /// </summary>
    Task<Dictionary<Guid, TrackingPoint>> GetLastLocationsAsync(
        IEnumerable<Guid> vendorIds, CancellationToken ct = default);
}
