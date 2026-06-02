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

    public async Task<ServiceResult<DashboardStatsResponse>> GetStatsAsync(
        Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var vendorFilter = currentRole == UserRole.Vendor ? currentUserId : (Guid?)null;
        var supervisorFilter = currentRole == UserRole.Supervisor ? currentUserId : (Guid?)null;

        var (visits, _) = await _visits.GetListAsync(
            1, 1000, null, vendorFilter, null, today, tomorrow, supervisorFilter, ct);
        var (orders, _) = await _orders.GetListAsync(
            1, 1000, null, vendorFilter, null, null, today, tomorrow, supervisorFilter, ct);

        // Approved orders for the day, then scoped in-memory.
        var approvedOrders = await _orders.GetApprovedWithLinesAsync(today, tomorrow, ct);
        approvedOrders = ScopeApprovedOrders(approvedOrders, vendorFilter, supervisorFilter, currentUserId);

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

    // GetApprovedWithLinesAsync currently does not accept role scoping, so we
    // filter in-memory. Cheap for daily snapshots; revisit if volume grows.
    private static List<Domain.Entities.Order> ScopeApprovedOrders(
        List<Domain.Entities.Order> orders,
        Guid? vendorFilter,
        Guid? supervisorFilter,
        Guid currentUserId)
    {
        if (vendorFilter.HasValue)
            return orders.Where(o => o.VendorId == vendorFilter.Value).ToList();
        if (supervisorFilter.HasValue)
            return orders.Where(o => o.Vendor != null && o.Vendor.SupervisorId == supervisorFilter.Value).ToList();
        return orders;
    }
}
