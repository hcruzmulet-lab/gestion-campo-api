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
}
