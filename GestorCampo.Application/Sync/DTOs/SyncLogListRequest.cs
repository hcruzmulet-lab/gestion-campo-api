namespace GestorCampo.Application.Sync.DTOs;

public class SyncLogListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Adapter { get; set; }
    public string? Status { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
