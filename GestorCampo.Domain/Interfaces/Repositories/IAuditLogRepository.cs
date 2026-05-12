using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Repositories;

public interface IAuditLogRepository
{
    Task<(List<AuditLog> items, int totalCount)> GetListAsync(
        int page, int pageSize,
        Guid? userId, string? module,
        DateTime? from, DateTime? to,
        CancellationToken ct = default);
}
