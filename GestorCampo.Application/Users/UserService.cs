using GestorCampo.Application.Common;
using GestorCampo.Application.Users.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using GestorCampo.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace GestorCampo.Application.Users;

public class UserService
{
    private readonly IUserRepository _users;
    private readonly IVisitRepository _visits;
    private readonly IPasswordService _password;
    private readonly IEmailService _email;
    private readonly int _emailVerificationExpiryHours;

    public UserService(
        IUserRepository users,
        IVisitRepository visits,
        IPasswordService password,
        IEmailService email,
        IConfiguration config)
    {
        _users = users;
        _visits = visits;
        _password = password;
        _email = email;
        _emailVerificationExpiryHours = int.Parse(config["Auth:EmailVerificationTokenExpiryHours"]!);
    }

    public async Task<ServiceResult<UserResponse>> CreateAsync(
        CreateUserRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        if (await _users.EmailExistsAsync(request.Email, ct))
            return ServiceResult<UserResponse>.Fail("El email ya está registrado");

        if (request.SupervisorId.HasValue)
        {
            var supervisor = await _users.GetByIdAsync(request.SupervisorId.Value, ct);
            if (supervisor == null || supervisor.Role != UserRole.Supervisor)
                return ServiceResult<UserResponse>.Fail("El supervisor especificado no es válido");
        }

        var verificationToken = _password.GenerateSecureToken();

        var user = new User
        {
            Name = request.Name,
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = _password.Hash(request.Password),
            Role = request.Role,
            Phone = request.Phone,
            Zone = request.Zone,
            IdNumber = request.IdNumber,
            EmployeeCode = request.EmployeeCode,
            Address = request.Address,
            SupervisorId = request.SupervisorId,
            EmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(_emailVerificationExpiryHours),
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };

        await _users.AddAsync(user, ct);
        await _email.SendEmailVerificationAsync(user.Email, user.Name, verificationToken, ct);

        return ServiceResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<ServiceResult<UserResponse>> GetByIdAsync(
        Guid id, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user == null) return ServiceResult<UserResponse>.Fail("Usuario no encontrado");

        if (currentRole == UserRole.Supervisor && user.SupervisorId != currentUserId)
            return ServiceResult<UserResponse>.Fail("No tiene acceso a este usuario");

        return ServiceResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<ServiceResult<PagedResult<UserResponse>>> GetListAsync(
        UserListRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var supervisorFilter = currentRole == UserRole.Supervisor ? currentUserId : (Guid?)null;

        var (items, totalCount) = await _users.GetListAsync(
            request.Page, pageSize,
            request.Role, request.IsActive, request.Search,
            supervisorFilter,
            request.OrderBy, request.Descending,
            ct);

        // Look up last visit per vendor in a single batched query.
        var vendorIds = items.Where(u => u.Role == UserRole.Vendor).Select(u => u.Id).ToList();
        var lastVisits = vendorIds.Count > 0
            ? await _visits.GetLastCheckinByVendorAsync(vendorIds, ct)
            : new Dictionary<Guid, DateTime>();

        return ServiceResult<PagedResult<UserResponse>>.Ok(new PagedResult<UserResponse>
        {
            Items = items.Select(u => ToResponse(u, lastVisits.TryGetValue(u.Id, out var lv) ? lv : (DateTime?)null)).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<UserResponse>> UpdateAsync(
        Guid id, UpdateUserRequest request, Guid currentUserId, UserRole currentRole, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user == null) return ServiceResult<UserResponse>.Fail("Usuario no encontrado");

        if (currentRole == UserRole.Supervisor && user.SupervisorId != currentUserId)
            return ServiceResult<UserResponse>.Fail("No tiene acceso a este usuario");

        if (request.Name != null) user.Name = request.Name;
        if (request.Phone != null) user.Phone = request.Phone;
        if (request.Zone != null) user.Zone = request.Zone;
        if (request.IdNumber != null) user.IdNumber = request.IdNumber;
        if (request.EmployeeCode != null) user.EmployeeCode = request.EmployeeCode;
        if (request.Address != null) user.Address = request.Address;
        if (request.PhotoUrl != null) user.PhotoUrl = request.PhotoUrl;
        if (request.SupervisorId.HasValue) user.SupervisorId = request.SupervisorId;
        user.UpdatedBy = currentUserId;

        await _users.UpdateAsync(user, ct);
        return ServiceResult<UserResponse>.Ok(ToResponse(user));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user == null) return ServiceResult.Fail("Usuario no encontrado");
        if (user.Id == currentUserId) return ServiceResult.Fail("No puedes eliminar tu propia cuenta");

        user.DeletedAt = DateTime.UtcNow;
        user.DeletedBy = currentUserId;
        user.IsActive = false;
        user.UpdatedBy = currentUserId;
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> BlockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user == null) return ServiceResult.Fail("Usuario no encontrado");

        user.LockedUntil = DateTime.UtcNow.AddYears(100);
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UnblockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user == null) return ServiceResult.Fail("Usuario no encontrado");

        user.LockedUntil = null;
        user.FailedAttempts = 0;
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> Toggle2FaAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct);
        if (user == null) return ServiceResult.Fail("Usuario no encontrado");

        user.TwoFaEnabled = !user.TwoFaEnabled;
        await _users.UpdateAsync(user, ct);
        return ServiceResult.Ok();
    }

    private static UserResponse ToResponse(User u) => ToResponse(u, null);

    private static UserResponse ToResponse(User u, DateTime? lastVisitAt) => new()
    {
        Id = u.Id,
        Name = u.Name,
        Email = u.Email,
        Role = u.Role,
        IsActive = u.IsActive,
        TwoFaEnabled = u.TwoFaEnabled,
        Phone = u.Phone,
        Zone = u.Zone,
        IdNumber = u.IdNumber,
        EmployeeCode = u.EmployeeCode,
        Address = u.Address,
        PhotoUrl = u.PhotoUrl,
        SupervisorId = u.SupervisorId,
        LastLoginAt = u.LastLoginAt,
        LastVisitAt = lastVisitAt,
        LockedUntil = u.LockedUntil,
        EmailVerified = u.EmailVerified,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };
}
