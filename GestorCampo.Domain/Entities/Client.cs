using GestorCampo.Domain.Common;

namespace GestorCampo.Domain.Entities;

public class Client : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Category { get; set; }
    public Guid? AssignedVendorId { get; set; }
    public User? AssignedVendor { get; set; }
    public string? ExternalId { get; set; }
    public string? Source { get; set; }
}
