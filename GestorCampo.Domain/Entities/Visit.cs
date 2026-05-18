using GestorCampo.Domain.Common;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Entities;

public class Visit : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid VendorId { get; set; }
    public User Vendor { get; set; } = null!;
    public Guid PlannedById { get; set; }
    public User PlannedBy { get; set; } = null!;
    public DateTime PlannedAt { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Planned;
    public DateTime? CheckinAt { get; set; }
    public DateTime? CheckoutAt { get; set; }
    public string? Notes { get; set; }
    public string? Result { get; set; }
    public string? Comment { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public Guid? RelatedOrderId { get; set; }
    public double? CheckInLat { get; set; }
    public double? CheckInLng { get; set; }
    public double? CheckOutLat { get; set; }
    public double? CheckOutLng { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public bool IsOutOfRange { get; set; }
    public int? OutOfRangeMeters { get; set; }
    public VisitNotCompletedReason? NotCompletedReason { get; set; }
    public string? NotCompletedReasonNote { get; set; }
    public List<VisitAttachment> Attachments { get; set; } = new();
}
