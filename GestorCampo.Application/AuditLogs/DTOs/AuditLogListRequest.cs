namespace GestorCampo.Application.AuditLogs.DTOs;

public class AuditLogListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? UserId { get; set; }
    public string? Module { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
