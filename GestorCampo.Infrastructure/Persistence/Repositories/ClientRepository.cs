using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _db;

    public ClientRepository(AppDbContext db) => _db = db;

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> TaxIdExistsAsync(string taxId, CancellationToken ct = default) =>
        _db.Clients.AnyAsync(c => c.TaxId == taxId, ct);

    public async Task<(List<Client> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        string? search, bool? isActive, string? category,
        Guid? assignedVendorId, CancellationToken ct = default)
    {
        var query = _db.Clients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || c.TaxId.Contains(search) || c.Email.Contains(search));
        if (isActive.HasValue)
            query = query.Where(c => c.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(c => c.Category == category);
        if (assignedVendorId.HasValue)
            query = query.Where(c => c.AssignedVendorId == assignedVendorId.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await _db.Clients.AddAsync(client, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        _db.Clients.Update(client);
        await _db.SaveChangesAsync(ct);
    }
}
