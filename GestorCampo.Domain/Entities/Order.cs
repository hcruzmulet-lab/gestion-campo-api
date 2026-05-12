using GestorCampo.Domain.Common;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Entities;

public class Order : BaseEntity
{
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public Guid VendorId { get; set; }
    public User Vendor { get; set; } = null!;
    public Guid? VisitId { get; set; }
    public Visit? Visit { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public string? RejectionComment { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}
