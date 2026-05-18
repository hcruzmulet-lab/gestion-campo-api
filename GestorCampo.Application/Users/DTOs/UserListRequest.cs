using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Users.DTOs;

public class UserListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
    public string? Search { get; set; }
    /// <summary>One of: name, email, lastLogin, createdAt. Default name.</summary>
    public string? OrderBy { get; set; }
    public bool Descending { get; set; } = false;
}
