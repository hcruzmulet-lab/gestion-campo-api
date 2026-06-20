using FluentAssertions;
using GestorCampo.Application.Common;
using GestorCampo.Application.Visits;
using GestorCampo.Application.Visits.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Visits;

public class VisitServiceTests
{
    private readonly Mock<IVisitRepository> _visitRepo = new();
    private readonly Mock<IClientRepository> _clientRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly VisitService _sut;

    public VisitServiceTests()
    {
        _sut = new VisitService(_visitRepo.Object, _clientRepo.Object, _userRepo.Object, new GeofenceService());
    }

    private Client BuildClient() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Farmacia Test",
        TaxId = "0912345678001",
        Address = "Av. Test 123",
        Phone = "0991234567",
        Email = "farmacia@test.com",
        IsActive = true
    };

    private User BuildVendor(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Vendor A",
        Email = "vendor@test.com",
        PasswordHash = "h",
        Role = UserRole.Vendor,
        IsActive = true
    };

    private Visit BuildVisit(Guid? vendorId = null, VisitStatus status = VisitStatus.Planned) => new()
    {
        Id = Guid.NewGuid(),
        ClientId = Guid.NewGuid(),
        Client = BuildClient(),
        VendorId = vendorId ?? Guid.NewGuid(),
        Vendor = BuildVendor(vendorId),
        PlannedById = Guid.NewGuid(),
        PlannedBy = BuildVendor(),
        PlannedAt = DateTime.UtcNow.AddDays(1),
        Status = status,
        IsActive = true
    };

    // --- Create ---

    [Fact]
    public async Task Create_ClientNotFound_ReturnsFail()
    {
        _clientRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = Guid.NewGuid(), PlannedAt = DateTime.UtcNow.AddDays(1) },
            Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Cliente no encontrado");
    }

    [Fact]
    public async Task Create_AsSupervisor_NoVendorId_ReturnsFail()
    {
        var client = BuildClient();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, VendorId = null, PlannedAt = DateTime.UtcNow.AddDays(1) },
            Guid.NewGuid(), UserRole.Supervisor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Debe especificar un vendedor");
    }

    [Fact]
    public async Task Create_VendorNotFound_ReturnsFail()
    {
        var client = BuildClient();
        var vendorId = Guid.NewGuid();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _userRepo.Setup(r => r.GetByIdAsync(vendorId, default)).ReturnsAsync((User?)null);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, VendorId = vendorId, PlannedAt = DateTime.UtcNow.AddDays(1) },
            Guid.NewGuid(), UserRole.Supervisor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("El vendedor especificado no es válido");
    }

    [Fact]
    public async Task Create_AsVendor_ReturnsOk()
    {
        var currentUserId = Guid.NewGuid();
        var client = BuildClient();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, PlannedAt = DateTime.UtcNow.AddDays(1) },
            currentUserId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        result.Data!.VendorId.Should().Be(currentUserId);
        _visitRepo.Verify(r => r.AddAsync(It.Is<Visit>(v => v.VendorId == currentUserId), default), Times.Once);
    }

    [Fact]
    public async Task Create_AsVendor_WithExistingInProgress_Succeeds()
    {
        // Planning is always allowed; the InProgress rule applies at check-in only.
        var currentUserId = Guid.NewGuid();
        var client = BuildClient();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _visitRepo.Setup(r => r.HasInProgressForVendorAsync(currentUserId, default)).ReturnsAsync(true);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, PlannedAt = DateTime.UtcNow.AddDays(1) },
            currentUserId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.AddAsync(It.IsAny<Visit>(), default), Times.Once);
        _visitRepo.Verify(r => r.HasInProgressForVendorAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_AsSupervisor_ValidVendor_ReturnsOk()
    {
        var supervisorId = Guid.NewGuid();
        var client = BuildClient();
        var vendor = BuildVendor();
        vendor.SupervisorId = supervisorId;
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _userRepo.Setup(r => r.GetByIdAsync(vendor.Id, default)).ReturnsAsync(vendor);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, VendorId = vendor.Id, PlannedAt = DateTime.UtcNow.AddDays(1) },
            supervisorId, UserRole.Supervisor);

        result.Succeeded.Should().BeTrue();
        result.Data!.VendorId.Should().Be(vendor.Id);
        _visitRepo.Verify(r => r.AddAsync(It.Is<Visit>(v => v.VendorId == vendor.Id), default), Times.Once);
    }

    [Fact]
    public async Task Create_AsSupervisor_VendorBelongsToOtherSupervisor_Fails()
    {
        var client = BuildClient();
        var vendor = BuildVendor();
        vendor.SupervisorId = Guid.NewGuid(); // different supervisor
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _userRepo.Setup(r => r.GetByIdAsync(vendor.Id, default)).ReturnsAsync(vendor);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, VendorId = vendor.Id, PlannedAt = DateTime.UtcNow.AddDays(1) },
            Guid.NewGuid(), UserRole.Supervisor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("equipo");
        _visitRepo.Verify(r => r.AddAsync(It.IsAny<Visit>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_AtomicCheckIn_SetsInProgressAndCoords()
    {
        var vendorId = Guid.NewGuid();
        var client = BuildClient();
        client.Lat = -1.0;
        client.Lng = -78.0;
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _visitRepo.Setup(r => r.HasInProgressForVendorAsync(vendorId, default)).ReturnsAsync(false);

        Visit? added = null;
        _visitRepo.Setup(r => r.AddAsync(It.IsAny<Visit>(), default))
            .Callback<Visit, CancellationToken>((v, _) => added = v)
            .Returns(Task.CompletedTask);

        var now = DateTime.UtcNow;
        var result = await _sut.CreateAsync(
            new CreateVisitRequest
            {
                ClientId = client.Id,
                PlannedAt = now,
                CheckInLat = -1.0,
                CheckInLng = -78.0,
                CheckinAt = now,
            },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        added.Should().NotBeNull();
        added!.Status.Should().Be(VisitStatus.InProgress);
        added.CheckInLat.Should().Be(-1.0);
        added.CheckInLng.Should().Be(-78.0);
        added.CheckinAt.Should().Be(now);
    }

    [Fact]
    public async Task Create_AtomicCheckIn_VendorAlreadyInProgress_Fails()
    {
        var vendorId = Guid.NewGuid();
        var client = BuildClient();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);
        _visitRepo.Setup(r => r.HasInProgressForVendorAsync(vendorId, default)).ReturnsAsync(true);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest
            {
                ClientId = client.Id,
                PlannedAt = DateTime.UtcNow,
                CheckInLat = -1.0,
                CheckInLng = -78.0,
                CheckinAt = DateTime.UtcNow,
            },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("visita en curso");
        _visitRepo.Verify(r => r.AddAsync(It.IsAny<Visit>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_NoAtomicCheckIn_StaysPlanned()
    {
        var vendorId = Guid.NewGuid();
        var client = BuildClient();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        Visit? added = null;
        _visitRepo.Setup(r => r.AddAsync(It.IsAny<Visit>(), default))
            .Callback<Visit, CancellationToken>((v, _) => added = v)
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(
            new CreateVisitRequest { ClientId = client.Id, PlannedAt = DateTime.UtcNow },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        added!.Status.Should().Be(VisitStatus.Planned);
        added.CheckinAt.Should().BeNull();
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_NotFound_ReturnsFail()
    {
        _visitRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Visit?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Visita no encontrada");
    }

    [Fact]
    public async Task GetById_VendorOwnVisit_ReturnsOk()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId: vendorId);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.GetByIdAsync(visit.Id, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(visit.Id);
    }

    [Fact]
    public async Task GetById_VendorOtherVisit_ReturnsFail()
    {
        var visit = BuildVisit(vendorId: Guid.NewGuid());
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.GetByIdAsync(visit.Id, Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta visita");
    }

    // --- CheckIn ---

    [Fact]
    public async Task CheckIn_NotFound_ReturnsFail()
    {
        _visitRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Visit?)null);

        var result = await _sut.CheckInAsync(Guid.NewGuid(), new CheckInRequest(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Visita no encontrada");
    }

    [Fact]
    public async Task CheckIn_NotVendorOwner_ReturnsFail()
    {
        var visit = BuildVisit(vendorId: Guid.NewGuid());
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.CheckInAsync(visit.Id, new CheckInRequest(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta visita");
    }

    [Fact]
    public async Task CheckIn_ValidPlannedVisit_SetsInProgressAndLocation()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId: vendorId, status: VisitStatus.Planned);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);
        _clientRepo.Setup(r => r.GetByIdAsync(visit.ClientId, default)).ReturnsAsync((Client?)null);

        var result = await _sut.CheckInAsync(visit.Id, new CheckInRequest { Lat = -0.23, Lng = -78.5 }, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(
            It.Is<Visit>(v =>
                v.Status == VisitStatus.InProgress &&
                v.CheckInLat == -0.23 &&
                v.CheckInLng == -78.5 &&
                v.UpdatedBy == vendorId),
            default), Times.Once);
    }

    [Fact]
    public async Task CheckIn_VendorWithExistingInProgress_Fails()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId: vendorId, status: VisitStatus.Planned);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);
        _visitRepo.Setup(r => r.HasInProgressForVendorAsync(vendorId, default)).ReturnsAsync(true);

        var result = await _sut.CheckInAsync(visit.Id, new CheckInRequest { Lat = -0.23, Lng = -78.5 }, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("visita en curso");
        _visitRepo.Verify(r => r.UpdateAsync(It.IsAny<Visit>(), default), Times.Never);
    }

    [Fact]
    public async Task CheckIn_AlreadyInProgress_ReturnsOk_WithoutReapplyingOrVendorRule()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.InProgress);
        visit.CheckInLat = -1.0; visit.CheckInLng = -2.0;
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.CheckInAsync(
            visit.Id, new CheckInRequest { Lat = -9.9, Lng = -9.9 }, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        visit.CheckInLat.Should().Be(-1.0); // retry payload ignored — persisted data kept
        visit.CheckInLng.Should().Be(-2.0);
        _visitRepo.Verify(r => r.UpdateAsync(It.IsAny<Visit>(), default), Times.Never);
        _visitRepo.Verify(r => r.HasInProgressForVendorAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    // --- CheckOut ---

    [Fact]
    public async Task CheckOut_NotFound_ReturnsFail()
    {
        _visitRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Visit?)null);

        var result = await _sut.CheckOutAsync(Guid.NewGuid(), new CheckOutRequest(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Visita no encontrada");
    }

    [Fact]
    public async Task CheckOut_NotVendorOwner_ReturnsFail()
    {
        var visit = BuildVisit(vendorId: Guid.NewGuid(), status: VisitStatus.InProgress);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.CheckOutAsync(visit.Id, new CheckOutRequest(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("No tiene acceso a esta visita");
    }

    [Fact]
    public async Task CheckOut_InProgress_Completed_ReturnsOk()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId: vendorId, status: VisitStatus.InProgress);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.CheckOutAsync(visit.Id,
            new CheckOutRequest { Lat = -0.23, Lng = -78.5 },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(
            It.Is<Visit>(v =>
                v.Status == VisitStatus.Completed &&
                v.CheckOutAt.HasValue &&
                v.CheckOutLat == -0.23 &&
                v.CheckOutLng == -78.5 &&
                v.UpdatedBy == vendorId),
            default), Times.Once);
    }

    // --- CheckIn/CheckOut geofence ---

    [Fact]
    public async Task CheckIn_VendorWithinRange_RecordsLocationAndTransitionsToInProgress()
    {
        var vendorId = Guid.NewGuid();
        var clientLat = -34.6037; var clientLng = -58.3816;
        var visit = BuildVisit(vendorId, VisitStatus.Planned);
        visit.ClientId = Guid.NewGuid();
        var client = new Client { Id = visit.ClientId, Lat = clientLat, Lng = clientLng };
        _visitRepo.Setup(v => v.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);
        _clientRepo.Setup(c => c.GetByIdAsync(visit.ClientId, default)).ReturnsAsync(client);

        var result = await _sut.CheckInAsync(visit.Id,
            new CheckInRequest { Lat = clientLat, Lng = clientLng },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        visit.CheckInLat.Should().Be(clientLat);
        visit.CheckInLng.Should().Be(clientLng);
        visit.IsOutOfRange.Should().BeFalse();
        visit.Status.Should().Be(VisitStatus.InProgress);
    }

    [Fact]
    public async Task CheckIn_VendorFarFromClient_MarksOutOfRange()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.Planned);
        visit.ClientId = Guid.NewGuid();
        var client = new Client { Id = visit.ClientId, Lat = -34.6037, Lng = -58.3816 };
        _visitRepo.Setup(v => v.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);
        _clientRepo.Setup(c => c.GetByIdAsync(visit.ClientId, default)).ReturnsAsync(client);

        var result = await _sut.CheckInAsync(visit.Id,
            new CheckInRequest { Lat = -34.5992, Lng = -58.3816 }, // ~500m north
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        visit.IsOutOfRange.Should().BeTrue();
        visit.OutOfRangeMeters.Should().BeInRange(490, 510);
    }

    [Fact]
    public async Task CheckOut_RecordsLocationAndCompletes()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.InProgress);
        _visitRepo.Setup(v => v.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.CheckOutAsync(visit.Id,
            new CheckOutRequest { Lat = -34.6, Lng = -58.4 },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        visit.Status.Should().Be(VisitStatus.Completed);
        visit.CheckOutAt.Should().NotBeNull();
        visit.CheckOutLat.Should().Be(-34.6);
        visit.CheckOutLng.Should().Be(-58.4);
    }

    [Fact]
    public async Task CheckOut_AlreadyCompleted_ReturnsOk_WithoutReapplying()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.Completed);
        visit.CheckOutLat = -5.0; visit.CheckOutLng = -6.0;
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.CheckOutAsync(
            visit.Id, new CheckOutRequest { Lat = -9.9, Lng = -9.9 }, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        visit.CheckOutLat.Should().Be(-5.0); // unchanged
        visit.CheckOutLng.Should().Be(-6.0);
        _visitRepo.Verify(r => r.UpdateAsync(It.IsAny<Visit>(), default), Times.Never);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_NotFound_ReturnsFail()
    {
        _visitRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Visit?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Supervisor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Visita no encontrada");
    }

    [Fact]
    public async Task Delete_ValidVisit_SoftDeletes()
    {
        var visit = BuildVisit();
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.DeleteAsync(visit.Id, Guid.NewGuid(), UserRole.SuperAdmin);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(
            It.Is<Visit>(v => v.DeletedAt.HasValue && !v.IsActive),
            default), Times.Once);
    }

    // --- MarkNotCompleted ---

    [Fact]
    public async Task MarkNotCompleted_NotFound_Fails()
    {
        _visitRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Visit?)null);

        var result = await _sut.MarkNotCompletedAsync(
            Guid.NewGuid(),
            new MarkNotCompletedRequest { Reason = VisitNotCompletedReason.ClientClosed },
            Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Visita no encontrada");
    }

    [Fact]
    public async Task MarkNotCompleted_VendorNotOwner_Fails()
    {
        var visit = BuildVisit(vendorId: Guid.NewGuid(), status: VisitStatus.Planned);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest { Reason = VisitNotCompletedReason.ClientClosed },
            Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("acceso");
    }

    [Fact]
    public async Task MarkNotCompleted_FromCompleted_Fails()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.Completed);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest { Reason = VisitNotCompletedReason.ClientClosed },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("estado");
    }

    [Fact]
    public async Task MarkNotCompleted_ReasonOtherWithoutNote_Fails()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.Planned);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest { Reason = VisitNotCompletedReason.Other, ReasonNote = null },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("nota");
    }

    [Fact]
    public async Task MarkNotCompleted_FromPlanned_SetsNotCompleted()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.Planned);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest { Reason = VisitNotCompletedReason.ClientClosed },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(
            It.Is<Visit>(v =>
                v.Status == VisitStatus.NotCompleted &&
                v.NotCompletedReason == VisitNotCompletedReason.ClientClosed),
            default), Times.Once);
    }

    [Fact]
    public async Task MarkNotCompleted_FromInProgress_SetsCheckoutTimestamp()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.InProgress);
        visit.CheckinAt = DateTime.UtcNow.AddMinutes(-10);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest
            {
                Reason = VisitNotCompletedReason.ClientRefused,
                Lat = -0.23, Lng = -78.5
            },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(
            It.Is<Visit>(v =>
                v.Status == VisitStatus.NotCompleted &&
                v.NotCompletedReason == VisitNotCompletedReason.ClientRefused &&
                v.CheckoutAt.HasValue &&
                v.CheckOutAt.HasValue &&
                v.CheckOutLat == -0.23 &&
                v.CheckOutLng == -78.5),
            default), Times.Once);
    }

    [Fact]
    public async Task MarkNotCompleted_ReasonOtherWithNote_Succeeds()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.Planned);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest
            {
                Reason = VisitNotCompletedReason.Other,
                ReasonNote = "Cliente avisó por WhatsApp"
            },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(
            It.Is<Visit>(v =>
                v.NotCompletedReason == VisitNotCompletedReason.Other &&
                v.NotCompletedReasonNote == "Cliente avisó por WhatsApp"),
            default), Times.Once);
    }

    [Fact]
    public async Task MarkNotCompleted_AlreadyNotCompleted_ReturnsOk()
    {
        var vendorId = Guid.NewGuid();
        var visit = BuildVisit(vendorId, VisitStatus.NotCompleted);
        _visitRepo.Setup(r => r.GetByIdAsync(visit.Id, default)).ReturnsAsync(visit);

        var result = await _sut.MarkNotCompletedAsync(
            visit.Id,
            new MarkNotCompletedRequest { Reason = VisitNotCompletedReason.ClientClosed },
            vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
        _visitRepo.Verify(r => r.UpdateAsync(It.IsAny<Visit>(), default), Times.Never);
    }
}
