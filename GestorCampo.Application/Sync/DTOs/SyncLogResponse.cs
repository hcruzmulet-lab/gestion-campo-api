namespace GestorCampo.Application.Sync.DTOs;

public class SyncLogResponse
{
    public Guid Id { get; set; }
    public string Adapter { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int? ItemsProcessed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
