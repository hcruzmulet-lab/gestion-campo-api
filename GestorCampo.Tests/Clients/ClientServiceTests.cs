using FluentAssertions;
using GestorCampo.Application.Clients;
using GestorCampo.Application.Clients.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Moq;

namespace GestorCampo.Tests.Clients;

public class ClientServiceTests
{
    private readonly Mock<IClientRepository> _clientRepo = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly ClientService _sut;

    public ClientServiceTests()
    {
        _sut = new ClientService(_clientRepo.Object, _userRepo.Object);
    }

    private Client BuildClient(Guid? assignedVendorId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Farmacia Test",
        TaxId = "0912345678001",
        Address = "Av. Test 123",
        Phone = "0991234567",
        Email = "farmacia@test.com",
        IsActive = true,
        AssignedVendorId = assignedVendorId
    };

    private User BuildVendor() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Vendor A",
        Email = "vendor@test.com",
        PasswordHash = "h",
        Role = UserRole.Vendor,
        IsActive = true
    };

    [Fact]
    public async Task Create_DuplicateTaxId_ReturnsFail()
    {
        _clientRepo.Setup(r => r.TaxIdExistsAsync("0912345678001", default)).ReturnsAsync(true);

        var result = await _sut.CreateAsync(
            new CreateClientRequest { TaxId = "0912345678001", Name = "A", Address = "B", Phone = "C", Email = "d@e.com" },
            Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("RUC");
    }

    [Fact]
    public async Task Create_InvalidVendor_ReturnsFail()
    {
        var vendorId = Guid.NewGuid();
        _clientRepo.Setup(r => r.TaxIdExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.GetByIdAsync(vendorId, default)).ReturnsAsync((User?)null);

        var result = await _sut.CreateAsync(
            new CreateClientRequest { TaxId = "123", Name = "A", Address = "B", Phone = "C", Email = "d@e.com", AssignedVendorId = vendorId },
            Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("vendedor");
    }

    [Fact]
    public async Task Create_VendorIdWithSupervisorRole_ReturnsFail()
    {
        var supervisorId = Guid.NewGuid();
        var supervisor = new User { Id = supervisorId, Role = UserRole.Supervisor, Name = "S", Email = "s@t.com", PasswordHash = "h" };
        _clientRepo.Setup(r => r.TaxIdExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.GetByIdAsync(supervisorId, default)).ReturnsAsync(supervisor);

        var result = await _sut.CreateAsync(
            new CreateClientRequest { TaxId = "123", Name = "A", Address = "B", Phone = "C", Email = "d@e.com", AssignedVendorId = supervisorId },
            Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("vendedor");
    }

    [Fact]
    public async Task Create_ValidData_SavesClientAndReturnsResponse()
    {
        _clientRepo.Setup(r => r.TaxIdExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);

        var result = await _sut.CreateAsync(
            new CreateClientRequest { TaxId = "123", Name = "Farmacia XYZ", Address = "Calle A", Phone = "09999", Email = "x@y.com" },
            Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        result.Data!.Name.Should().Be("Farmacia XYZ");
        _clientRepo.Verify(r => r.AddAsync(It.IsAny<Client>(), default), Times.Once);
    }

    [Fact]
    public async Task GetById_NotFound_ReturnsFail()
    {
        _clientRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_VendorAccessesUnownedClient_ReturnsFail()
    {
        var vendorId = Guid.NewGuid();
        var client = BuildClient(assignedVendorId: Guid.NewGuid()); // different vendor
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.GetByIdAsync(client.Id, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_VendorAccessesOwnClient_ReturnsOk()
    {
        var vendorId = Guid.NewGuid();
        var client = BuildClient(assignedVendorId: vendorId);
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.GetByIdAsync(client.Id, vendorId, UserRole.Vendor);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_SupervisorAccessesAnyClient_ReturnsOk()
    {
        var client = BuildClient(assignedVendorId: Guid.NewGuid());
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.GetByIdAsync(client.Id, Guid.NewGuid(), UserRole.Supervisor);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task GetList_Vendor_ForcesVendorIdAsFilter()
    {
        var vendorId = Guid.NewGuid();
        _clientRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Client>(), 0));

        await _sut.GetListAsync(new ClientListRequest(), vendorId, UserRole.Vendor);

        _clientRepo.Verify(r => r.GetListAsync(1, 20, null, null, null, vendorId, default), Times.Once);
    }

    [Fact]
    public async Task GetList_SuperAdmin_PassesRequestVendorFilter()
    {
        var filterVendorId = Guid.NewGuid();
        _clientRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Client>(), 0));

        await _sut.GetListAsync(
            new ClientListRequest { AssignedVendorId = filterVendorId },
            Guid.NewGuid(), UserRole.SuperAdmin);

        _clientRepo.Verify(r => r.GetListAsync(1, 20, null, null, null, filterVendorId, default), Times.Once);
    }

    [Fact]
    public async Task Delete_NotFound_ReturnsFail()
    {
        _clientRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Client?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ValidClient_SoftDeletesAndDeactivates()
    {
        var client = BuildClient();
        _clientRepo.Setup(r => r.GetByIdAsync(client.Id, default)).ReturnsAsync(client);

        var result = await _sut.DeleteAsync(client.Id, Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        _clientRepo.Verify(r => r.UpdateAsync(
            It.Is<Client>(c => c.DeletedAt.HasValue && !c.IsActive), default), Times.Once);
    }
}
