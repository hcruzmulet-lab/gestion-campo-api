using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Users.DTOs;

public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? Phone { get; set; }
    public string? Zone { get; set; }
    public string? IdNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Address { get; set; }
    public Guid? SupervisorId { get; set; }
}
