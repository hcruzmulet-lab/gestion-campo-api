namespace GestorCampo.Application.Products.DTOs;

public class ProductListRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public string? Category { get; set; }
}
