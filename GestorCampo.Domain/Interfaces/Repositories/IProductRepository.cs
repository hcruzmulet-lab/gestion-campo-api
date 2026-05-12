using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    Task<(List<Product> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        string? search, bool? isActive, string? category,
        CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
}
