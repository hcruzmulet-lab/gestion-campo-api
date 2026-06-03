namespace GestorCampo.Application.Visits.DTOs;

public class CreateVisitRequest
{
    public Guid ClientId { get; set; }
    public Guid? VendorId { get; set; } // Required when Supervisor/SuperAdmin creates
    public DateTime PlannedAt { get; set; }
    public string? Notes { get; set; }

    // Optional atomic check-in. When all three fields are present, the visit
    // is created as InProgress with these coords/timestamp, skipping the
    // separate POST /api/visits/{id}/check-in step. Used by the mobile
    // ad-hoc-visit offline flow so a single outbox item covers both actions.
    public double? CheckInLat { get; set; }
    public double? CheckInLng { get; set; }
    public DateTime? CheckinAt { get; set; }
}
