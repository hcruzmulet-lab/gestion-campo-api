namespace GestorCampo.Application.Tracking.DTOs;

public class TrackingQueryRequest
{
    public Guid VendorId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
}
