using GestorCampo.Application.Common;
using GestorCampo.Application.Dashboard.DTOs;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Dashboard;

public class DashboardService
{
    private readonly IVisitRepository _visits;
    private readonly IOrderRepository _orders;

    public DashboardService(IVisitRepository visits, IOrderRepository orders)
    {
        _visits = visits;
        _orders = orders;
    }

    public async Task<ServiceResult<DashboardStatsResponse>> GetStatsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var (visits, _) = await _visits.GetListAsync(1, 1000, null, null, null, today, tomorrow, ct);
        var (orders, _) = await _orders.GetListAsync(1, 1000, null, null, null, today, tomorrow, ct);

        var stats = new DashboardStatsResponse
        {
            Visits = new VisitStats
            {
                Planned = visits.Count(v => v.Status == VisitStatus.Planned),
                InProgress = visits.Count(v => v.Status == VisitStatus.InProgress),
                Completed = visits.Count(v => v.Status == VisitStatus.Completed),
                NotCompleted = visits.Count(v => v.Status == VisitStatus.NotCompleted)
            },
            Orders = new OrderStats
            {
                Draft = orders.Count(o => o.Status == OrderStatus.Draft),
                Sent = orders.Count(o => o.Status == OrderStatus.Sent),
                Approved = orders.Count(o => o.Status == OrderStatus.Approved),
                Rejected = orders.Count(o => o.Status == OrderStatus.Rejected),
                Delivered = orders.Count(o => o.Status == OrderStatus.Delivered)
            },
            ActiveVendorsToday = visits.Select(v => v.VendorId).Distinct().Count()
        };

        return ServiceResult<DashboardStatsResponse>.Ok(stats);
    }
}
