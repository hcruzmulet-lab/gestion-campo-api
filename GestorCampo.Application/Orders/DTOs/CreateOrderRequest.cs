namespace GestorCampo.Application.Orders.DTOs;

public class CreateOrderRequest
{
    public Guid ClientId { get; set; }
    public Guid? VisitId { get; set; }
    public List<CreateOrderLineRequest> Lines { get; set; } = new();
}
