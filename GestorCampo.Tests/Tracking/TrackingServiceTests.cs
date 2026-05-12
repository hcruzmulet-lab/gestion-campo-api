using FluentAssertions;
using GestorCampo.Application.Tracking;
using GestorCampo.Application.Tracking.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Tracking;

public class TrackingServiceTests
{
    private readonly Mock<ITrackingRepository> _trackingRepo = new();
    private readonly TrackingService _sut;

    public TrackingServiceTests()
    {
        _sut = new TrackingService(_trackingRepo.Object);
    }

    [Fact]
    public async Task AddPoints_ValidPoints_CallsAddRangeOnce()
    {
        var vendorId = Guid.NewGuid();
        var request = new BulkTrackingRequest
        {
            Points = new List<TrackingPointRequest>
            {
                new() { Lat = -0.23, Lng = -78.5, CapturedAt = DateTime.UtcNow.AddMinutes(-5) },
                new() { Lat = -0.24, Lng = -78.51, CapturedAt = DateTime.UtcNow.AddMinutes(-3) }
            }
        };

        var result = await _sut.AddPointsAsync(request, vendorId);

        result.Succeeded.Should().BeTrue();
        _trackingRepo.Verify(r => r.AddRangeAsync(
            It.Is<IEnumerable<TrackingPoint>>(pts =>
                pts.Count() == 2 &&
                pts.All(p => p.VendorId == vendorId && p.SyncedAt != default)),
            default), Times.Once);
    }

    [Fact]
    public async Task GetTrail_ValidQuery_ReturnsPointsInOrder()
    {
        var vendorId = Guid.NewGuid();
        var from = DateTime.UtcNow.AddHours(-8);
        var to = DateTime.UtcNow;

        var stored = new List<TrackingPoint>
        {
            new() { Id = Guid.NewGuid(), VendorId = vendorId, Lat = -0.23, Lng = -78.5, CapturedAt = from.AddMinutes(10), SyncedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), VendorId = vendorId, Lat = -0.24, Lng = -78.51, CapturedAt = from.AddMinutes(20), SyncedAt = DateTime.UtcNow }
        };

        _trackingRepo.Setup(r => r.GetByVendorAndDateRangeAsync(vendorId, from, to, default))
            .ReturnsAsync(stored);

        var result = await _sut.GetTrailAsync(new TrackingQueryRequest { VendorId = vendorId, From = from, To = to });

        result.Succeeded.Should().BeTrue();
        result.Data.Should().HaveCount(2);
        result.Data![0].Lat.Should().Be(-0.23);
        result.Data![1].Lat.Should().Be(-0.24);
    }
}
