namespace GestorCampo.Application.Tracking.DTOs;

public class BulkTrackingRequest
{
    public List<TrackingPointRequest> Points { get; set; } = new();
}
