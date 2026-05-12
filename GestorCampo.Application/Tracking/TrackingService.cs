using GestorCampo.Application.Common;
using GestorCampo.Application.Tracking.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Tracking;

public class TrackingService
{
    private readonly ITrackingRepository _tracking;

    public TrackingService(ITrackingRepository tracking) => _tracking = tracking;

    public async Task<ServiceResult> AddPointsAsync(
        BulkTrackingRequest request, Guid vendorId, CancellationToken ct = default)
    {
        var points = request.Points.Select(p => new TrackingPoint
        {
            VendorId = vendorId,
            Lat = p.Lat,
            Lng = p.Lng,
            CapturedAt = p.CapturedAt,
            SyncedAt = DateTime.UtcNow
        });

        await _tracking.AddRangeAsync(points, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<List<TrackingPointResponse>>> GetTrailAsync(
        TrackingQueryRequest request, CancellationToken ct = default)
    {
        var points = await _tracking.GetByVendorAndDateRangeAsync(
            request.VendorId, request.From, request.To, ct);

        var response = points.Select(p => new TrackingPointResponse
        {
            Lat = p.Lat,
            Lng = p.Lng,
            CapturedAt = p.CapturedAt
        }).ToList();

        return ServiceResult<List<TrackingPointResponse>>.Ok(response);
    }
}
