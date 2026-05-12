namespace GestorCampo.Application.Clients.DTOs;

public class UpdateClientRequest
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Category { get; set; }
    public Guid? AssignedVendorId { get; set; }
}
