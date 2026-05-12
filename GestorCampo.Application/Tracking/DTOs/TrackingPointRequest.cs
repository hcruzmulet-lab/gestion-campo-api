namespace GestorCampo.Application.Tracking.DTOs;

public class TrackingPointRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime CapturedAt { get; set; }
}
