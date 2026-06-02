using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Orders
            .Include(o => o.Client)
            .Include(o => o.Vendor)
            .Include(o => o.Lines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<(List<Order> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        OrderStatus? status, Guid? vendorId, Guid? clientId, Guid? visitId,
        DateTime? from, DateTime? to,
        Guid? supervisorOfVendor,
        CancellationToken ct = default)
    {
        var query = _db.Orders
            .Include(o => o.Client)
            .Include(o => o.Vendor)
            .Include(o => o.Lines)
                .ThenInclude(l => l.Product)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);
        if (vendorId.HasValue)
            query = query.Where(o => o.VendorId == vendorId.Value);
        if (clientId.HasValue)
            query = query.Where(o => o.ClientId == clientId.Value);
        if (visitId.HasValue)
            query = query.Where(o => o.VisitId == visitId.Value);
        if (from.HasValue)
            query = query.Where(o => o.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(o => o.CreatedAt <= to.Value);
        if (supervisorOfVendor.HasValue)
            query = query.Where(o => o.Vendor.SupervisorId == supervisorOfVendor.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<List<Order>> GetApprovedWithLinesAsync(
        DateTime from, DateTime to, CancellationToken ct = default) =>
        _db.Orders
            .Include(o => o.Vendor)
            .Include(o => o.Lines)
            .Where(o => o.Status == OrderStatus.Approved
                     && o.CreatedAt >= from
                     && o.CreatedAt <= to)
            .ToListAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
    {
        await _db.Orders.AddAsync(order, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        // Entities loaded via GetByIdAsync are already tracked. Calling
        // .Update() on a tracked entity that just mutated its Lines collection
        // makes EF emit an UPDATE whose WHERE clause silently fails to match
        // (DbUpdateConcurrencyException, "0 rows affected"). Only call Update
        // when the entity is detached.
        var entry = _db.Entry(order);
        if (entry.State == EntityState.Detached)
            _db.Orders.Update(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task ReplaceLinesAsync(
        Order order, IEnumerable<OrderLine> newLines, Guid updatedBy, CancellationToken ct = default)
    {
        // Load existing lines into the tracker explicitly, then RemoveRange
        // so EF emits DELETE statements instead of trying to UPDATE orphans.
        var existing = await _db.Set<OrderLine>()
            .Where(l => l.OrderId == order.Id)
            .ToListAsync(ct);
        _db.Set<OrderLine>().RemoveRange(existing);

        foreach (var l in newLines)
        {
            l.OrderId = order.Id;
            // Detach product navigation so EF doesn't try to insert it.
            l.Product = null!;
            _db.Set<OrderLine>().Add(l);
        }

        var entry = _db.Entry(order);
        order.UpdatedBy = updatedBy;
        order.UpdatedAt = DateTime.UtcNow;
        if (entry.State == EntityState.Detached)
            _db.Orders.Update(order);

        await _db.SaveChangesAsync(ct);
    }
}
