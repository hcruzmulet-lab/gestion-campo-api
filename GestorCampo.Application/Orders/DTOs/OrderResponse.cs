using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Orders.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? ClientAddress { get; set; }
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public Guid? VisitId { get; set; }
    public OrderStatus Status { get; set; }
    public string? RejectionComment { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<OrderLineResponse> Lines { get; set; } = new();
}
