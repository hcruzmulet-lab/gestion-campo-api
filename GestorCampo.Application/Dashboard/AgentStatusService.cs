using GestorCampo.Application.Dashboard.DTOs;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Dashboard;

public class AgentStatusService
{
    private readonly IUserRepository _users;
    private readonly IVisitRepository _visits;
    private readonly ITrackingRepository _tracking;

    public AgentStatusService(
        IUserRepository users,
        IVisitRepository visits,
        ITrackingRepository tracking)
    {
        _users = users;
        _visits = visits;
        _tracking = tracking;
    }

    public async Task<List<AgentStatusDto>> GetAgentStatusesAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var (vendors, _) = await _users.GetListAsync(1, 200, UserRole.Vendor, true, null, null, ct);
        if (vendors.Count == 0) return new List<AgentStatusDto>();

        var (visits, _) = await _visits.GetListAsync(1, 2000, null, null, null, today, tomorrow, ct);
        var lastLocations = await _tracking.GetLastLocationsAsync(vendors.Select(v => v.Id), ct);

        var visitsByVendor = visits
            .GroupBy(v => v.VendorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return vendors.Select(vendor =>
        {
            var vendorVisits = visitsByVendor.GetValueOrDefault(vendor.Id, new());
            var inProgress = vendorVisits.FirstOrDefault(v => v.Status == VisitStatus.InProgress);
            lastLocations.TryGetValue(vendor.Id, out var loc);

            return new AgentStatusDto
            {
                VendorId = vendor.Id,
                VendorName = vendor.Name,
                TotalVisitsToday = vendorVisits.Count,
                CompletedVisitsToday = vendorVisits.Count(v => v.Status == VisitStatus.Completed),
                CurrentVisitClient = inProgress?.Client?.Name,
                LastLocation = loc is null ? null : new LastLocationDto
                {
                    Lat = loc.Lat,
                    Lng = loc.Lng,
                    CapturedAt = loc.CapturedAt
                }
            };
        }).ToList();
    }
}
