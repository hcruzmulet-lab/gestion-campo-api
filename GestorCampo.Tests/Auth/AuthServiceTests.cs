using FluentAssertions;
using GestorCampo.Application.Auth;
using GestorCampo.Application.Auth.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GestorCampo.Tests.Auth;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IRefreshTokenRepository> _tokenRepo = new();
    private readonly Mock<IJwtService> _jwt = new();
    private readonly Mock<IPasswordService> _password = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IOtpService> _otp = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenDays"] = "7",
                ["Auth:MaxFailedAttempts"] = "5",
                ["Auth:LockoutMinutes"] = "15",
                ["Auth:OtpExpiryMinutes"] = "5",
                ["Auth:PasswordResetTokenExpiryHours"] = "24",
                ["Auth:EmailVerificationTokenExpiryHours"] = "48"
            })
            .Build();

        _sut = new AuthService(_userRepo.Object, _tokenRepo.Object, _jwt.Object,
            _password.Object, _email.Object, _otp.Object, config);
    }

    private User BuildUser(bool emailVerified = true, bool isActive = true,
        bool is2faEnabled = false, bool isLocked = false) => new()
    {
        Id = Guid.NewGuid(),
        Email = "vendor@test.com",
        PasswordHash = "hashed",
        Role = UserRole.Vendor,
        EmailVerified = emailVerified,
        IsActive = isActive,
        TwoFaEnabled = is2faEnabled,
        LockedUntil = isLocked ? DateTime.UtcNow.AddMinutes(10) : null
    };

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);
        _password.Setup(p => p.Verify("pass123", user.PasswordHash)).Returns(true);
        _jwt.Setup(j => j.GenerateAccessToken(user)).Returns("access-token");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns(("refresh-token", "refresh-hash"));
        _tokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default)).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "pass123"));

        result.Succeeded.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.RefreshToken.Should().Be("refresh-token");
        result.Data.Requires2fa.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsFail()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("unknown@test.com", default)).ReturnsAsync((User?)null);

        var result = await _sut.LoginAsync(new LoginRequest("unknown@test.com", "pass"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsFail()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);
        _password.Setup(p => p.Verify("wrong", user.PasswordHash)).Returns(false);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "wrong"));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithLockedAccount_ReturnsFail()
    {
        var user = BuildUser(isLocked: true);
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "pass"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("bloqueada");
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsFail()
    {
        var user = BuildUser(isActive: false);
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "pass"));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithUnverifiedEmail_ReturnsFail()
    {
        var user = BuildUser(emailVerified: false);
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "pass"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Contain("verificar");
    }

    [Fact]
    public async Task Login_With2faEnabled_ReturnsRequires2fa()
    {
        var user = BuildUser(is2faEnabled: true);
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);
        _password.Setup(p => p.Verify("pass123", user.PasswordHash)).Returns(true);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.LoginAsync(new LoginRequest(user.Email, "pass123"));

        result.Succeeded.Should().BeTrue();
        result.Data!.Requires2fa.Should().BeTrue();
        result.Data.AccessToken.Should().BeEmpty();
        result.Data.RefreshToken.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_ExceedingMaxAttempts_LocksAccount()
    {
        var user = BuildUser();
        user.FailedAttempts = 4;
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);
        _password.Setup(p => p.Verify("wrong", user.PasswordHash)).Returns(false);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        await _sut.LoginAsync(new LoginRequest(user.Email, "wrong"));

        user.LockedUntil.Should().NotBeNull();
        user.LockedUntil.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        var user = BuildUser();
        var storedToken = new RefreshToken
        {
            UserId = user.Id, User = user,
            TokenHash = "hash", Family = "family-1",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _jwt.Setup(j => j.GenerateAccessToken(user)).Returns("new-access");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns(("new-refresh", "new-hash"));
        _tokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), default)).ReturnsAsync(storedToken);
        _tokenRepo.Setup(r => r.RevokeAsync(storedToken, default)).Returns(Task.CompletedTask);
        _tokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default)).Returns(Task.CompletedTask);

        var result = await _sut.RefreshAsync(new RefreshRequest("old-refresh"));

        result.Succeeded.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("new-access");
        result.Data.RefreshToken.Should().Be("new-refresh");
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsFail()
    {
        var expiredToken = new RefreshToken
        {
            TokenHash = "hash", Family = "family-1",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _tokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), default)).ReturnsAsync(expiredToken);

        var result = await _sut.RefreshAsync(new RefreshRequest("old-token"));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_RevokesEntireFamily()
    {
        var revokedToken = new RefreshToken
        {
            TokenHash = "hash", Family = "family-1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };
        _tokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), default)).ReturnsAsync(revokedToken);
        _tokenRepo.Setup(r => r.RevokeAllByFamilyAsync("family-1", default)).Returns(Task.CompletedTask);

        var result = await _sut.RefreshAsync(new RefreshRequest("reused-token"));

        result.Succeeded.Should().BeFalse();
        _tokenRepo.Verify(r => r.RevokeAllByFamilyAsync("family-1", default), Times.Once);
    }

    [Fact]
    public async Task ForgotPassword_WithKnownEmail_SendsEmail()
    {
        var user = BuildUser();
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email, default)).ReturnsAsync(user);
        _password.Setup(p => p.GenerateSecureToken()).Returns("reset-token");
        _email.Setup(e => e.SendPasswordResetAsync(user.Email, user.Name, "reset-token", default))
            .Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest(user.Email));

        result.Succeeded.Should().BeTrue();
        _email.Verify(e => e.SendPasswordResetAsync(user.Email, user.Name, "reset-token", default), Times.Once);
        user.PasswordResetToken.Should().Be("reset-token");
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_SucceedsSilently()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("unknown@test.com", default)).ReturnsAsync((User?)null);

        var result = await _sut.ForgotPasswordAsync(new ForgotPasswordRequest("unknown@test.com"));

        result.Succeeded.Should().BeTrue();
        _email.Verify(e => e.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPassword()
    {
        var user = BuildUser();
        user.PasswordResetToken = "valid-token";
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);
        _userRepo.Setup(r => r.GetByPasswordResetTokenAsync("valid-token", default)).ReturnsAsync(user);
        _password.Setup(p => p.Hash("new-password")).Returns("new-hash");
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest("valid-token", "new-password"));

        result.Succeeded.Should().BeTrue();
        user.PasswordHash.Should().Be("new-hash");
        user.PasswordResetToken.Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_ReturnsFail()
    {
        var user = BuildUser();
        user.PasswordResetToken = "expired-token";
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(-1);
        _userRepo.Setup(r => r.GetByPasswordResetTokenAsync("expired-token", default)).ReturnsAsync(user);

        var result = await _sut.ResetPasswordAsync(new ResetPasswordRequest("expired-token", "new-pass"));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_ActivatesAccount()
    {
        var user = BuildUser(emailVerified: false);
        user.EmailVerificationToken = "verify-token";
        user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
        _userRepo.Setup(r => r.GetByEmailVerificationTokenAsync("verify-token", default)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.VerifyEmailAsync(new VerifyEmailRequest("verify-token"));

        result.Succeeded.Should().BeTrue();
        user.EmailVerified.Should().BeTrue();
        user.EmailVerificationToken.Should().BeNull();
    }

    [Fact]
    public async Task Send2fa_With2faEnabled_SendsOtp()
    {
        var user = BuildUser(is2faEnabled: true);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _otp.Setup(o => o.GenerateCode()).Returns("123456");
        _otp.Setup(o => o.StoreAsync(user.Id, "123456", "2fa", 5, default)).Returns(Task.CompletedTask);
        _email.Setup(e => e.SendOtpAsync(user.Email, user.Name, "123456", default)).Returns(Task.CompletedTask);

        var result = await _sut.Send2faAsync(new Send2faRequest(user.Id));

        result.Succeeded.Should().BeTrue();
        _email.Verify(e => e.SendOtpAsync(user.Email, user.Name, "123456", default), Times.Once);
    }

    [Fact]
    public async Task Verify2fa_WithValidCode_ReturnsTokens()
    {
        var user = BuildUser(is2faEnabled: true);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _otp.Setup(o => o.ValidateAsync(user.Id, "123456", "2fa", default)).ReturnsAsync(true);
        _jwt.Setup(j => j.GenerateAccessToken(user)).Returns("access-token");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns(("refresh-token", "refresh-hash"));
        _tokenRepo.Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default)).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.UpdateAsync(user, default)).Returns(Task.CompletedTask);

        var result = await _sut.Verify2faAsync(new Verify2faRequest(user.Id, "123456"));

        result.Succeeded.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("access-token");
        result.Data.Requires2fa.Should().BeFalse();
    }

    [Fact]
    public async Task Verify2fa_WithInvalidCode_ReturnsFail()
    {
        var user = BuildUser(is2faEnabled: true);
        _userRepo.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        _otp.Setup(o => o.ValidateAsync(user.Id, "wrong", "2fa", default)).ReturnsAsync(false);

        var result = await _sut.Verify2faAsync(new Verify2faRequest(user.Id, "wrong"));

        result.Succeeded.Should().BeFalse();
    }
}
