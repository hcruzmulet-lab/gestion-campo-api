using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Users.DTOs;

public class UpdateUserRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Zone { get; set; }
    public string? IdNumber { get; set; }
    public string? EmployeeCode { get; set; }
    public string? Address { get; set; }
    public string? PhotoUrl { get; set; }
    public Guid? SupervisorId { get; set; }
    public UserRole? Role { get; set; }
}
