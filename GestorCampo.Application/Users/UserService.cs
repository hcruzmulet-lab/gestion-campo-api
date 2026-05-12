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
    private readonly IPasswordService _password;
    private readonly IEmailService _email;
    private readonly int _emailVerificationExpiryHours;

    public UserService(
        IUserRepository users,
        IPasswordService password,
        IEmailService email,
        IConfiguration config)
    {
        _users = users;
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
            supervisorFilter, ct);

        return ServiceResult<PagedResult<UserResponse>>.Ok(new PagedResult<UserResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    private static UserResponse ToResponse(User u) => new()
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
        LockedUntil = u.LockedUntil,
        EmailVerified = u.EmailVerified,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt
    };
}
