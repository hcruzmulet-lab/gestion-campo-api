using GestorCampo.Domain.Common;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? Phone { get; set; }
    public string? Zone { get; set; }
    public string? IdNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public Guid? SupervisorId { get; set; }
    public User? Supervisor { get; set; }
    public bool TwoFaEnabled { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationTokenExpiry { get; set; }
    public bool EmailVerified { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiry { get; set; }

    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;
}
