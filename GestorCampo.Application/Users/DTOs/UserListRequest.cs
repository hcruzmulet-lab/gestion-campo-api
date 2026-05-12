using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Users.DTOs;

public class UserListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
}
