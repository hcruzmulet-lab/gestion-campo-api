namespace GestorCampo.Domain.Entities;

public class SyncLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Adapter { get; set; } = string.Empty;   // e.g. "ClientProvider"
    public string Entity { get; set; } = string.Empty;    // e.g. "clients"
    public string Status { get; set; } = string.Empty;    // "running" | "success" | "failed"
    public string? Error { get; set; }
    public int? ItemsProcessed { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
