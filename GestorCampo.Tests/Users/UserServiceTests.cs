using FluentAssertions;
using GestorCampo.Application.Users;
using GestorCampo.Application.Users.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GestorCampo.Tests.Users;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IVisitRepository> _visitRepo = new();
    private readonly Mock<IPasswordService> _password = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:EmailVerificationTokenExpiryHours"] = "48"
            })
            .Build();

        _visitRepo
            .Setup(r => r.GetLastCheckinByVendorAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        _sut = new UserService(_userRepo.Object, _visitRepo.Object, _password.Object, _email.Object, config);
    }

    private User BuildUser(UserRole role = UserRole.Vendor, Guid? supervisorId = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test User",
        Email = "test@test.com",
        PasswordHash = "hash",
        Role = role,
        SupervisorId = supervisorId,
        EmailVerified = true,
        IsActive = true
    };

    // ---- Create ----

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsFail()
    {
        _userRepo.Setup(r => r.EmailExistsAsync("test@test.com", default)).ReturnsAsync(true);
        var request = new CreateUserRequest { Email = "test@test.com", Name = "A", Password = "pass", Role = UserRole.Vendor };

        var result = await _sut.CreateAsync(request, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("email");
    }

    [Fact]
    public async Task Create_InvalidSupervisor_ReturnsFail()
    {
        var supervisorId = Guid.NewGuid();
        _userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.GetByIdAsync(supervisorId, default)).ReturnsAsync((User?)null);
        var request = new CreateUserRequest { Email = "new@test.com", Name = "A", Password = "pass", Role = UserRole.Vendor, SupervisorId = supervisorId };

        var result = await _sut.CreateAsync(request, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("supervisor");
    }

    [Fact]
    public async Task Create_SupervisorIdPointsToNonSupervisor_ReturnsFail()
    {
        var notSupervisorId = Guid.NewGuid();
        var vendor = BuildUser(UserRole.Vendor);
        vendor.Id = notSupervisorId;
        _userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.GetByIdAsync(notSupervisorId, default)).ReturnsAsync(vendor);
        var request = new CreateUserRequest { Email = "new@test.com", Name = "A", Password = "pass", Role = UserRole.Vendor, SupervisorId = notSupervisorId };

        var result = await _sut.CreateAsync(request, Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("supervisor");
    }

    [Fact]
    public async Task Create_ValidData_SavesUserAndSendsVerificationEmail()
    {
        _userRepo.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _password.Setup(p => p.Hash(It.IsAny<string>())).Returns("hashed");
        _password.Setup(p => p.GenerateSecureToken()).Returns("token123");
        var request = new CreateUserRequest { Email = "new@test.com", Name = "Ana", Password = "pass", Role = UserRole.Vendor };
        var currentUserId = Guid.NewGuid();

        var result = await _sut.CreateAsync(request, currentUserId);

        result.Succeeded.Should().BeTrue();
        result.Data!.Email.Should().Be("new@test.com");
        result.Data.EmailVerified.Should().BeFalse();
        _userRepo.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Once);
        _email.Verify(e => e.SendEmailVerificationAsync("new@test.com", "Ana", "token123", default), Times.Once);
    }

    // ---- GetById ----

    [Fact]
    public async Task GetById_NotFound_ReturnsFail()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User?)null);

        var result = await _sut.GetByIdAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.SuperAdmin);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_SupervisorAccessesUnownedVendor_ReturnsFail()
    {
        var supervisorId = Guid.NewGuid();
        var vendor = BuildUser(supervisorId: Guid.NewGuid()); // different supervisor
        _userRepo.Setup(r => r.GetByIdAsync(vendor.Id, default)).ReturnsAsync(vendor);

        var result = await _sut.GetByIdAsync(vendor.Id, supervisorId, UserRole.Supervisor);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_SupervisorAccessesOwnVendor_ReturnsOk()
    {
        var supervisorId = Guid.NewGuid();
        var vendor = BuildUser(supervisorId: supervisorId);
        _userRepo.Setup(r => r.GetByIdAsync(vendor.Id, default)).ReturnsAsync(vendor);

        var result = await _sut.GetByIdAsync(vendor.Id, supervisorId, UserRole.Supervisor);

        result.Succeeded.Should().BeTrue();
        result.Data!.Id.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task GetById_SuperAdminAccessesAnyUser_ReturnsOk()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.GetByIdAsync(user.Id, Guid.NewGuid(), UserRole.SuperAdmin);

        result.Succeeded.Should().BeTrue();
    }

    // ---- GetList ----

    [Fact]
    public async Task GetList_SuperAdmin_PassesNullSupervisorFilter()
    {
        _userRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<UserRole?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<User>(), 0));

        await _sut.GetListAsync(new UserListRequest(), Guid.NewGuid(), UserRole.SuperAdmin);

        _userRepo.Verify(r => r.GetListAsync(1, 20, null, null, null, null, null, false, default), Times.Once);
    }

    [Fact]
    public async Task GetList_Supervisor_PassesSupervisorIdAsFilter()
    {
        var supervisorId = Guid.NewGuid();
        _userRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<UserRole?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<User>(), 0));

        await _sut.GetListAsync(new UserListRequest(), supervisorId, UserRole.Supervisor);

        _userRepo.Verify(r => r.GetListAsync(1, 20, null, null, null, supervisorId, null, false, default), Times.Once);
    }

    [Fact]
    public async Task GetList_PageSizeOver100_CappedAt100()
    {
        _userRepo.Setup(r => r.GetListAsync(
            It.IsAny<int>(), It.IsAny<int>(),
            It.IsAny<UserRole?>(), It.IsAny<bool?>(), It.IsAny<string?>(),
            It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<User>(), 0));

        await _sut.GetListAsync(new UserListRequest { PageSize = 500 }, Guid.NewGuid(), UserRole.SuperAdmin);

        _userRepo.Verify(r => r.GetListAsync(1, 100, null, null, null, null, null, false, default), Times.Once);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_NotFound_ReturnsFail()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User?)null);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), new UpdateUserRequest(), Guid.NewGuid(), UserRole.SuperAdmin);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Update_SupervisorUpdatesUnownedVendor_ReturnsFail()
    {
        var supervisorId = Guid.NewGuid();
        var vendor = BuildUser(supervisorId: Guid.NewGuid()); // different supervisor
        _userRepo.Setup(r => r.GetByIdAsync(vendor.Id, default)).ReturnsAsync(vendor);

        var result = await _sut.UpdateAsync(vendor.Id, new UpdateUserRequest { Name = "New" }, supervisorId, UserRole.Supervisor);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Update_ValidData_UpdatesNameAndCallsRepository()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.UpdateAsync(user.Id, new UpdateUserRequest { Name = "Updated" }, Guid.NewGuid(), UserRole.SuperAdmin);

        result.Succeeded.Should().BeTrue();
        result.Data!.Name.Should().Be("Updated");
        _userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Name == "Updated"), default), Times.Once);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_OwnAccount_ReturnsFail()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, Name = "A", Email = "a@b.com", PasswordHash = "h", Role = UserRole.SuperAdmin };
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);

        var result = await _sut.DeleteAsync(userId, userId);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ValidUser_SoftDeletesAndDeactivates()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.DeleteAsync(user.Id, Guid.NewGuid());

        result.Succeeded.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.DeletedAt.HasValue && !u.IsActive), default), Times.Once);
    }

    // ---- Block / Unblock ----

    [Fact]
    public async Task Block_ValidUser_SetsLockedUntilFarFuture()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.BlockAsync(user.Id);

        result.Succeeded.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.LockedUntil > DateTime.UtcNow.AddYears(99)), default), Times.Once);
    }

    [Fact]
    public async Task Block_NotFound_ReturnsFail()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((User?)null);

        var result = await _sut.BlockAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Unblock_ValidUser_ClearsLockAndResetAttempts()
    {
        var user = BuildUser();
        user.LockedUntil = DateTime.UtcNow.AddYears(100);
        user.FailedAttempts = 5;
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.UnblockAsync(user.Id);

        result.Succeeded.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(
            It.Is<User>(u => u.LockedUntil == null && u.FailedAttempts == 0), default), Times.Once);
    }

    // ---- Toggle2FA ----

    [Fact]
    public async Task Toggle2FA_WhenDisabled_Enables()
    {
        var user = BuildUser();
        user.TwoFaEnabled = false;
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.Toggle2FaAsync(user.Id);

        result.Succeeded.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => u.TwoFaEnabled), default), Times.Once);
    }

    [Fact]
    public async Task Toggle2FA_WhenEnabled_Disables()
    {
        var user = BuildUser();
        user.TwoFaEnabled = true;
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);

        var result = await _sut.Toggle2FaAsync(user.Id);

        result.Succeeded.Should().BeTrue();
        _userRepo.Verify(r => r.UpdateAsync(It.Is<User>(u => !u.TwoFaEnabled), default), Times.Once);
    }
}
