using GestorCampo.Domain.Enums;

namespace GestorCampo.Application.Orders.DTOs;

public class OrderListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public OrderStatus? Status { get; set; }
    public Guid? VendorId { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
