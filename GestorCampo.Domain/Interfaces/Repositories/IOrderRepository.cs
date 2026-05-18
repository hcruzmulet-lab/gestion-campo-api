using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default); // includes Lines + Product
    Task<(List<Order> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        OrderStatus? status, Guid? vendorId, Guid? clientId, Guid? visitId,
        DateTime? from, DateTime? to, CancellationToken ct = default);
    /// <summary>Returns Approved orders in the given UTC range, including their Lines.</summary>
    Task<List<Order>> GetApprovedWithLinesAsync(
        DateTime from, DateTime to, CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    /// <summary>
    /// Atomically replaces a Draft order's lines: deletes all existing lines
    /// and inserts the provided ones. Use this instead of mutating Order.Lines
    /// directly on a tracked entity (which leaves orphans / triggers
    /// concurrency exceptions on save).
    /// </summary>
    Task ReplaceLinesAsync(Order order, IEnumerable<OrderLine> newLines, Guid updatedBy, CancellationToken ct = default);
}
