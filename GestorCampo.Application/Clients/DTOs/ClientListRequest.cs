namespace GestorCampo.Application.Clients.DTOs;

public class ClientListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public string? Category { get; set; }
    public Guid? AssignedVendorId { get; set; }
}
