using GestorCampo.Application.Common;
using GestorCampo.Application.Products.DTOs;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.Products;

public class ProductService
{
    private readonly IProductRepository _products;

    public ProductService(IProductRepository products) => _products = products;

    public async Task<ServiceResult<ProductResponse>> CreateAsync(
        CreateProductRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        if (await _products.CodeExistsAsync(request.Code, ct))
            return ServiceResult<ProductResponse>.Fail("El código de producto ya está registrado");

        var product = new Product
        {
            Name = request.Name,
            Code = request.Code,
            Price = request.Price,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            Category = request.Category,
            Stock = request.Stock,
            CreatedBy = currentUserId,
            UpdatedBy = currentUserId
        };

        await _products.AddAsync(product, ct);
        return ServiceResult<ProductResponse>.Ok(ToResponse(product));
    }

    public async Task<ServiceResult<ProductResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product == null) return ServiceResult<ProductResponse>.Fail("Producto no encontrado");
        return ServiceResult<ProductResponse>.Ok(ToResponse(product));
    }

    public async Task<ServiceResult<PagedResult<ProductResponse>>> GetListAsync(
        ProductListRequest request, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var (items, totalCount) = await _products.GetListAsync(
            request.Page, pageSize,
            request.Search, request.IsActive, request.Category, ct);

        return ServiceResult<PagedResult<ProductResponse>>.Ok(new PagedResult<ProductResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    public async Task<ServiceResult<ProductResponse>> UpdateAsync(
        Guid id, UpdateProductRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product == null) return ServiceResult<ProductResponse>.Fail("Producto no encontrado");

        if (request.Name != null) product.Name = request.Name;
        if (request.Price.HasValue) product.Price = request.Price.Value;
        if (request.Description != null) product.Description = request.Description;
        if (request.ImageUrl != null) product.ImageUrl = request.ImageUrl;
        if (request.Category != null) product.Category = request.Category;
        if (request.Stock.HasValue) product.Stock = request.Stock.Value;
        product.UpdatedBy = currentUserId;

        await _products.UpdateAsync(product, ct);
        return ServiceResult<ProductResponse>.Ok(ToResponse(product));
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, Guid currentUserId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(id, ct);
        if (product == null) return ServiceResult.Fail("Producto no encontrado");

        product.DeletedAt = DateTime.UtcNow;
        product.DeletedBy = currentUserId;
        product.IsActive = false;
        product.UpdatedBy = currentUserId;
        await _products.UpdateAsync(product, ct);
        return ServiceResult.Ok();
    }

    private static ProductResponse ToResponse(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Code = p.Code,
        Price = p.Price,
        Description = p.Description,
        ImageUrl = p.ImageUrl,
        Category = p.Category,
        Stock = p.Stock,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt
    };
}
