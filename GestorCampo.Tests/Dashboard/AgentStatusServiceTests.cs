using FluentAssertions;
using GestorCampo.Application.Dashboard;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Dashboard;

public class AgentStatusServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IVisitRepository> _visitRepo = new();
    private readonly Mock<ITrackingRepository> _trackingRepo = new();
    private readonly AgentStatusService _sut;

    public AgentStatusServiceTests()
    {
        _sut = new AgentStatusService(_userRepo.Object, _visitRepo.Object, _trackingRepo.Object);
    }

    private void SetupVendors(params User[] vendors)
    {
        _userRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            UserRole.Vendor, true, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((vendors.ToList(), vendors.Length));
    }

    private void SetupVisits(params Visit[] visits)
    {
        _visitRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            null, null, null,
            It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((visits.ToList(), visits.Length));
    }

    private void SetupLastLocations(Dictionary<Guid, TrackingPoint> locations)
    {
        _trackingRepo.Setup(r => r.GetLastLocationsAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(locations);
    }

    [Fact]
    public async Task GetAgentStatuses_NoVendors_ReturnsEmpty()
    {
        SetupVendors();
        SetupVisits();
        SetupLastLocations(new Dictionary<Guid, TrackingPoint>());

        var result = await _sut.GetAgentStatusesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAgentStatuses_VendorWithVisits_CountsCorrectly()
    {
        var vendorId = Guid.NewGuid();
        SetupVendors(new User { Id = vendorId, Name = "Juan R.", Role = UserRole.Vendor, IsActive = true, Email = "j@g.com", PasswordHash = "x" });
        SetupVisits(
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Completed, PlannedAt = DateTime.UtcNow },
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Completed, PlannedAt = DateTime.UtcNow },
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.Planned,   PlannedAt = DateTime.UtcNow }
        );
        SetupLastLocations(new Dictionary<Guid, TrackingPoint>());

        var result = await _sut.GetAgentStatusesAsync();

        var agent = result.Should().ContainSingle().Subject;
        agent.TotalVisitsToday.Should().Be(3);
        agent.CompletedVisitsToday.Should().Be(2);
    }

    [Fact]
    public async Task GetAgentStatuses_VendorWithInProgressVisit_ReturnsClientName()
    {
        var vendorId = Guid.NewGuid();
        var client = new Client { Id = Guid.NewGuid(), Name = "Supermercado XYZ", TaxId = "x", Address = "x", Phone = "x", Email = "x@x.com" };
        SetupVendors(new User { Id = vendorId, Name = "Ana M.", Role = UserRole.Vendor, IsActive = true, Email = "a@g.com", PasswordHash = "x" });
        SetupVisits(
            new Visit { Id = Guid.NewGuid(), VendorId = vendorId, Status = VisitStatus.InProgress, PlannedAt = DateTime.UtcNow, Client = client, ClientId = client.Id }
        );
        SetupLastLocations(new Dictionary<Guid, TrackingPoint>());

        var result = await _sut.GetAgentStatusesAsync();

        result.Single().CurrentVisitClient.Should().Be("Supermercado XYZ");
    }

    [Fact]
    public async Task GetAgentStatuses_VendorWithLastLocation_MapsCorrectly()
    {
        var vendorId = Guid.NewGuid();
        var capturedAt = DateTime.UtcNow.AddMinutes(-15);
        SetupVendors(new User { Id = vendorId, Name = "Pedro L.", Role = UserRole.Vendor, IsActive = true, Email = "p@g.com", PasswordHash = "x" });
        SetupVisits();
        SetupLastLocations(new Dictionary<Guid, TrackingPoint>
        {
            [vendorId] = new TrackingPoint { VendorId = vendorId, Lat = -34.603, Lng = -58.381, CapturedAt = capturedAt }
        });

        var result = await _sut.GetAgentStatusesAsync();

        var agent = result.Single();
        agent.LastLocation.Should().NotBeNull();
        agent.LastLocation!.Lat.Should().Be(-34.603);
        agent.LastLocation.Lng.Should().Be(-58.381);
        agent.LastLocation.CapturedAt.Should().Be(capturedAt);
    }

    [Fact]
    public async Task GetAgentStatuses_VendorWithNoTracking_LastLocationIsNull()
    {
        var vendorId = Guid.NewGuid();
        SetupVendors(new User { Id = vendorId, Name = "Sofia T.", Role = UserRole.Vendor, IsActive = true, Email = "s@g.com", PasswordHash = "x" });
        SetupVisits();
        SetupLastLocations(new Dictionary<Guid, TrackingPoint>());

        var result = await _sut.GetAgentStatusesAsync();

        result.Single().LastLocation.Should().BeNull();
    }
}
