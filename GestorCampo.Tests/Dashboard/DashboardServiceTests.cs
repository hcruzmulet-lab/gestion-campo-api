using FluentAssertions;
using GestorCampo.Application.Dashboard;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Dashboard;

public class DashboardServiceTests
{
    private readonly Mock<IVisitRepository> _visitRepo = new();
    private readonly Mock<IOrderRepository> _orderRepo = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests()
    {
        _sut = new DashboardService(_visitRepo.Object, _orderRepo.Object);
    }

    private void SetupVisits(params Visit[] visits)
    {
        _visitRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<VisitStatus?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((visits.ToList(), visits.Length));
    }

    private void SetupOrders(params Order[] orders)
    {
        _orderRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<OrderStatus?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((orders.ToList(), orders.Length));
    }

    [Fact]
    public async Task GetStats_NoData_ReturnsAllZeros()
    {
        SetupVisits();
        SetupOrders();

        var result = await _sut.GetStatsAsync();

        result.Succeeded.Should().BeTrue();
        result.Data!.Visits.Total.Should().Be(0);
        result.Data.Orders.Total.Should().Be(0);
        result.Data.ActiveVendorsToday.Should().Be(0);
    }

    [Fact]
    public async Task GetStats_WithVisits_CountsByStatus()
    {
        var vendorId = Guid.NewGuid();
        SetupVisits(
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Planned, PlannedAt = DateTime.UtcNow },
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Completed, PlannedAt = DateTime.UtcNow },
            new Visit { Id = Guid.NewGuid(), VendorId = Guid.NewGuid(), Status = VisitStatus.Completed, PlannedAt = DateTime.UtcNow }
        );
        SetupOrders();

        var result = await _sut.GetStatsAsync();

        result.Data!.Visits.Planned.Should().Be(1);
        result.Data.Visits.Completed.Should().Be(2);
        result.Data.Visits.Total.Should().Be(3);
        result.Data.ActiveVendorsToday.Should().Be(2); // 2 distinct vendors
    }

    [Fact]
    public async Task GetStats_WithOrders_CountsByStatus()
    {
        SetupVisits();
        SetupOrders(
            new Order { Id = Guid.NewGuid(), VendorId = Guid.NewGuid(), Status = OrderStatus.Sent },
            new Order { Id = Guid.NewGuid(), VendorId = Guid.NewGuid(), Status = OrderStatus.Approved },
            new Order { Id = Guid.NewGuid(), VendorId = Guid.NewGuid(), Status = OrderStatus.Approved }
        );

        var result = await _sut.GetStatsAsync();

        result.Data!.Orders.Sent.Should().Be(1);
        result.Data.Orders.Approved.Should().Be(2);
        result.Data.Orders.Total.Should().Be(3);
    }

    [Fact]
    public async Task GetStats_ActiveVendors_DistinctByVendorId()
    {
        var vendorId = Guid.NewGuid();
        SetupVisits(
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Planned, PlannedAt = DateTime.UtcNow },
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Completed, PlannedAt = DateTime.UtcNow }
        );
        SetupOrders();

        var result = await _sut.GetStatsAsync();

        result.Data!.ActiveVendorsToday.Should().Be(1); // same vendorId twice → 1 distinct
    }
}
