using GestorCampo.Domain.Common;

namespace GestorCampo.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }
    public int? Stock { get; set; }
    public string? ExternalId { get; set; }
    public string? Source { get; set; }
}
