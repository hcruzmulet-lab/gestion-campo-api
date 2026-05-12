namespace GestorCampo.Application.Clients.DTOs;

public class ClientResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Category { get; set; }
    public Guid? AssignedVendorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
