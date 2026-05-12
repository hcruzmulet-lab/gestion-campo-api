using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetByExternalIdAsync(string externalId, CancellationToken ct = default) =>
        _db.Products.FirstOrDefaultAsync(p => p.ExternalId == externalId, ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) =>
        _db.Products.AnyAsync(p => p.Code == code, ct);

    public async Task<(List<Product> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        string? search, bool? isActive, string? category,
        CancellationToken ct = default)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.Code.Contains(search));
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _db.Products.AddAsync(product, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Update(product);
        await _db.SaveChangesAsync(ct);
    }
}
