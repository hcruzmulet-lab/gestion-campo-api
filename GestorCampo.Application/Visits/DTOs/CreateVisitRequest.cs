namespace GestorCampo.Application.Visits.DTOs;

public class CreateVisitRequest
{
    public Guid ClientId { get; set; }
    public Guid? VendorId { get; set; } // Required when Supervisor/SuperAdmin creates
    public DateTime PlannedAt { get; set; }
    public string? Notes { get; set; }
}
