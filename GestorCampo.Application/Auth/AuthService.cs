using System.Security.Cryptography;
using System.Text;
using GestorCampo.Application.Auth.DTOs;
using GestorCampo.Application.Common;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace GestorCampo.Application.Auth;

public class AuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IJwtService _jwt;
    private readonly IPasswordService _password;
    private readonly IEmailService _email;
    private readonly IOtpService _otp;
    private readonly int _refreshTokenDays;
    private readonly int _maxFailedAttempts;
    private readonly int _lockoutMinutes;
    private readonly int _otpExpiryMinutes;
    private readonly int _passwordResetExpiryHours;
    private readonly int _emailVerificationExpiryHours;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository tokens,
        IJwtService jwt,
        IPasswordService password,
        IEmailService email,
        IOtpService otp,
        IConfiguration config)
    {
        _users = users;
        _tokens = tokens;
        _jwt = jwt;
        _password = password;
        _email = email;
        _otp = otp;
        _refreshTokenDays = int.Parse(config["Jwt:RefreshTokenDays"]!);
        _maxFailedAttempts = int.Parse(config["Auth:MaxFailedAttempts"]!);
        _lockoutMinutes = int.Parse(config["Auth:LockoutMinutes"]!);
        _otpExpiryMinutes = int.Parse(config["Auth:OtpExpiryMinutes"]!);
        _passwordResetExpiryHours = int.Parse(config["Auth:PasswordResetTokenExpiryHours"]!);
        _emailVerificationExpiryHours = int.Parse(config["Auth:EmailVerificationTokenExpiryHours"]!);
    }

    public async Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);
        if (user == null)
            return ServiceResult<LoginResponse>.Fail("Credenciales inválidas");

        if (!user.IsActive)
            return ServiceResult<LoginResponse>.Fail("Cuenta inactiva");

        if (user.IsLocked)
            return ServiceResult<LoginResponse>.Fail("Cuenta bloqueada temporalmente");

        if (!user.EmailVerified)
            return ServiceResult<LoginResponse>.Fail("Debes verificar tu email antes de iniciar sesión");

        if (!_password.Verify(request.Password, user.PasswordHash))
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= _maxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(_lockoutMinutes);
                user.FailedAttempts = 0;
            }
            await _users.UpdateAsync(user, ct);
            return ServiceResult<LoginResponse>.Fail("Credenciales inválidas");
        }

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);

        if (user.TwoFaEnabled)
            return ServiceResult<LoginResponse>.Ok(new LoginResponse(string.Empty, string.Empty, true, user.Id));

        return ServiceResult<LoginResponse>.Ok(await IssueTokensAsync(user, ct));
    }

    public async Task<ServiceResult<RefreshResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.RefreshToken)));

        var stored = await _tokens.GetByTokenHashAsync(tokenHash, ct);
        if (stored == null)
            return ServiceResult<RefreshResponse>.Fail("Token inválido");

        if (stored.IsRevoked)
        {
            await _tokens.RevokeAllByFamilyAsync(stored.Family, ct);
            return ServiceResult<RefreshResponse>.Fail("Token comprometido — sesión revocada");
        }

        if (stored.IsExpired)
            return ServiceResult<RefreshResponse>.Fail("Token expirado");

        await _tokens.RevokeAsync(stored, ct);

        var (newToken, newHash) = _jwt.GenerateRefreshToken();
        await _tokens.AddAsync(new RefreshToken
        {
            UserId = stored.UserId,
            TokenHash = newHash,
            Family = stored.Family,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
            CreatedBy = stored.UserId,
            UpdatedBy = stored.UserId
        }, ct);

        var accessToken = _jwt.GenerateAccessToken(stored.User);
        return ServiceResult<RefreshResponse>.Ok(new RefreshResponse(accessToken, newToken));
    }

    public async Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenHash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        var stored = await _tokens.GetByTokenHashAsync(tokenHash, ct);
        if (stored == null)
            return ServiceResult.Ok();

        await _tokens.RevokeAllByFamilyAsync(stored.Family, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(request.Email, ct);
        if (user == null)
            return ServiceResult.Ok();

        var token = _password.GenerateSecureToken();
        user.PasswordResetToken = token;
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(_passwordResetExpiryHours);
        await _users.UpdateAsync(user, ct);
        await _email.SendPasswordResetAsync(user.Email, user.Name, token, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByPasswordResetTokenAsync(request.Token, ct);
        if (user == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            return ServiceResult.Fail("Token inválido o expirado");

        user.PasswordHash = _password.Hash(request.NewPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ChangePasswordAsync(
        Guid currentUserId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return ServiceResult.Fail("La nueva contraseña debe tener al menos 8 caracteres");

        var user = await _users.GetByIdAsync(currentUserId, ct);
        if (user == null) return ServiceResult.Fail("Usuario no encontrado");

        if (!_password.Verify(request.CurrentPassword, user.PasswordHash))
            return ServiceResult.Fail("La contraseña actual es incorrecta");

        user.PasswordHash = _password.Hash(request.NewPassword);
        user.UpdatedBy = currentUserId;
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailVerificationTokenAsync(request.Token, ct);
        if (user == null || user.EmailVerificationTokenExpiry < DateTime.UtcNow)
            return ServiceResult.Fail("Token inválido o expirado");

        user.EmailVerified = true;
        user.EmailVerificationToken = null;
        user.EmailVerificationTokenExpiry = null;
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> Send2faAsync(Send2faRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user == null || !user.TwoFaEnabled)
            return ServiceResult.Fail("Usuario no encontrado o 2FA no habilitado");

        var code = _otp.GenerateCode();
        await _otp.StoreAsync(user.Id, code, "2fa", _otpExpiryMinutes, ct);
        await _email.SendOtpAsync(user.Email, user.Name, code, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<LoginResponse>> Verify2faAsync(Verify2faRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        if (user == null)
            return ServiceResult<LoginResponse>.Fail("Usuario no encontrado");

        var valid = await _otp.ValidateAsync(user.Id, request.Code, "2fa", ct);
        if (!valid)
            return ServiceResult<LoginResponse>.Fail("Código inválido o expirado");

        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user, ct);
        var tokens = await IssueTokensAsync(user, ct);
        return ServiceResult<LoginResponse>.Ok(tokens);
    }

    private async Task<LoginResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var (refreshToken, refreshHash) = _jwt.GenerateRefreshToken();
        var family = Guid.NewGuid().ToString();

        await _tokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            Family = family,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenDays),
            CreatedBy = user.Id,
            UpdatedBy = user.Id
        }, ct);

        return new LoginResponse(accessToken, refreshToken, false, user.Id);
    }
}
