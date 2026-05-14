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
        var approvedOrders = await _orders.GetApprovedWithLinesAsync(today, tomorrow, ct);

        var doneCount = visits.Count(v => v.Status is VisitStatus.Completed or VisitStatus.NotCompleted);
        var approvedValue = approvedOrders
            .SelectMany(o => o.Lines)
            .Sum(l => l.Quantity * l.UnitPrice * (1 - l.Discount));

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
            ActiveVendorsToday = visits.Select(v => v.VendorId).Distinct().Count(),
            ConversionRate = visits.Count > 0
                ? visits.Count(v => v.RelatedOrderId.HasValue) / (float)visits.Count
                : 0f,
            TotalApprovedValue = approvedValue,
            VisitCompletionRate = doneCount > 0
                ? visits.Count(v => v.Status == VisitStatus.Completed) / (float)doneCount
                : 0f
        };

        return ServiceResult<DashboardStatsResponse>.Ok(stats);
    }
}
