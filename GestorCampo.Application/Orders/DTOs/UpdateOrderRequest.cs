namespace GestorCampo.Application.Orders.DTOs;

public class UpdateOrderRequest
{
    public List<CreateOrderLineRequest> Lines { get; set; } = new();
}
