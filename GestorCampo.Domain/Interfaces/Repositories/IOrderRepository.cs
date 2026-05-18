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
}
