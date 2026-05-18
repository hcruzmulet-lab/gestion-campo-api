using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Users.DTOs;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool TwoFaEnabled { get; set; }
    public string? Phone { get; set; }
    public string? Zone { get; set; }
    public string? IdNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public Guid? SupervisorId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastVisitAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
