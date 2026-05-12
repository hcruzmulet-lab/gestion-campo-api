namespace GestorCampo.Application.Tracking.DTOs;

public class TrackingPointResponse
{
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime CapturedAt { get; set; }
}
