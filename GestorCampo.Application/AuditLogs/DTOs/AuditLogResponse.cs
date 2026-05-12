namespace GestorCampo.Application.AuditLogs.DTOs;

public class AuditLogResponse
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
