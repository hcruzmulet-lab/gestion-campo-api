using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IVisitAttachmentRepository
{
    Task<VisitAttachment?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<VisitAttachment>> GetByVisitIdAsync(Guid visitId, CancellationToken ct = default);
    Task<int> CountByVisitIdAsync(Guid visitId, CancellationToken ct = default);
    Task AddAsync(VisitAttachment attachment, CancellationToken ct = default);
    Task DeleteAsync(VisitAttachment attachment, CancellationToken ct = default);
}
