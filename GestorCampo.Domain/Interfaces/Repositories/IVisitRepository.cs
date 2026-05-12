using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IVisitRepository
{
    Task<Visit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<Visit> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        VisitStatus? status, Guid? vendorId, Guid? clientId,
        DateTime? from, DateTime? to, CancellationToken ct = default);
    Task AddAsync(Visit visit, CancellationToken ct = default);
    Task UpdateAsync(Visit visit, CancellationToken ct = default);
}
