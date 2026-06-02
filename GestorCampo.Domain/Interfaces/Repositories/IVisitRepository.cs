using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Enums;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IVisitRepository
{
    Task<Visit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(List<Visit> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        VisitStatus? status, Guid? vendorId, Guid? clientId,
        DateTime? from, DateTime? to,
        Guid? supervisorOfVendor,
        CancellationToken ct = default);
    Task AddAsync(Visit visit, CancellationToken ct = default);
    Task UpdateAsync(Visit visit, CancellationToken ct = default);
    Task<bool> HasInProgressForVendorAsync(Guid vendorId, CancellationToken ct = default);
    /// <summary>Returns the most-recent CheckinAt per vendor for the given ids. Vendors with no check-in are absent from the dictionary.</summary>
    Task<Dictionary<Guid, DateTime>> GetLastCheckinByVendorAsync(IEnumerable<Guid> vendorIds, CancellationToken ct = default);
}
