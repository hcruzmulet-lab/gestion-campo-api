using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestorCampo.Infrastructure.Persistence.Repositories;

public class VisitAttachmentRepository : IVisitAttachmentRepository
{
    private readonly AppDbContext _db;
    public VisitAttachmentRepository(AppDbContext db) => _db = db;

    public Task<VisitAttachment?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.VisitAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<List<VisitAttachment>> GetByVisitIdAsync(Guid visitId, CancellationToken ct = default) =>
        _db.VisitAttachments.Where(a => a.VisitId == visitId)
                            .OrderBy(a => a.CreatedAt)
                            .ToListAsync(ct);

    public Task<int> CountByVisitIdAsync(Guid visitId, CancellationToken ct = default) =>
        _db.VisitAttachments.CountAsync(a => a.VisitId == visitId, ct);

    public async Task AddAsync(VisitAttachment attachment, CancellationToken ct = default)
    {
        _db.VisitAttachments.Add(attachment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(VisitAttachment attachment, CancellationToken ct = default)
    {
        attachment.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
