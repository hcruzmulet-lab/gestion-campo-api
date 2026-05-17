using FluentAssertions;
using GestorCampo.Application.Common;
using Xunit;

namespace GestorCampo.Tests.Visits;

public class GeofenceServiceTests
{
    [Fact]
    public void SamePoint_ReturnsZeroDistance_WithinRange()
    {
        var svc = new GeofenceService();
        var (within, distance) = svc.Compute(-34.6037, -58.3816, -34.6037, -58.3816, 200);
        within.Should().BeTrue();
        distance.Should().Be(0);
    }

    [Fact]
    public void OneHundredMetersApart_WithinTwoHundredThreshold()
    {
        var svc = new GeofenceService();
        // ~100m north of base point
        var (within, distance) = svc.Compute(-34.6037, -58.3816, -34.6028, -58.3816, 200);
        within.Should().BeTrue();
        distance.Should().BeInRange(95, 110);
    }

    [Fact]
    public void FiveHundredMetersApart_OutsideTwoHundredThreshold()
    {
        var svc = new GeofenceService();
        // ~500m north
        var (within, distance) = svc.Compute(-34.6037, -58.3816, -34.5992, -58.3816, 200);
        within.Should().BeFalse();
        distance.Should().BeInRange(490, 510);
    }
}
