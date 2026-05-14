namespace GestorCampo.Application.Dashboard.DTOs;

public class AgentStatusDto
{
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public int TotalVisitsToday { get; set; }
    public int CompletedVisitsToday { get; set; }
    public string? CurrentVisitClient { get; set; }
    public LastLocationDto? LastLocation { get; set; }
}

public class LastLocationDto
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime CapturedAt { get; set; }
}
