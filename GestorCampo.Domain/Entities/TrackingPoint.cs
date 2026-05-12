namespace GestorCampo.Domain.Entities;

public class TrackingPoint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VendorId { get; set; }
    public User Vendor { get; set; } = null!;
    public double Lat { get; set; }
    public double Lng { get; set; }
    public DateTime CapturedAt { get; set; }
    public DateTime SyncedAt { get; set; }
}
