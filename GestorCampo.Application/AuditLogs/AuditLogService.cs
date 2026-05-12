using GestorCampo.Application.AuditLogs.DTOs;
using GestorCampo.Application.Common;
using GestorCampo.Domain.Entities;
using GestorCampo.Domain.Interfaces.Repositories;

namespace GestorCampo.Application.AuditLogs;

public class AuditLogService
{
    private readonly IAuditLogRepository _auditLogs;

    public AuditLogService(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<ServiceResult<PagedResult<AuditLogResponse>>> GetListAsync(
        AuditLogListRequest request, CancellationToken ct = default)
    {
        var pageSize = Math.Min(request.PageSize, 100);
        var (items, totalCount) = await _auditLogs.GetListAsync(
            request.Page, pageSize,
            request.UserId, request.Module,
            request.From, request.To, ct);

        return ServiceResult<PagedResult<AuditLogResponse>>.Ok(new PagedResult<AuditLogResponse>
        {
            Items = items.Select(ToResponse).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = pageSize
        });
    }

    private static AuditLogResponse ToResponse(AuditLog l) => new()
    {
        Id = l.Id,
        UserId = l.UserId,
        Action = l.Action,
        Module = l.Module,
        Entity = l.Entity,
        EntityId = l.EntityId,
        IpAddress = l.IpAddress,
        UserAgent = l.UserAgent,
        CreatedAt = l.CreatedAt
    };
}
