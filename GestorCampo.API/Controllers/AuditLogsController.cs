// GestorCampo.API/Controllers/AuditLogsController.cs
using GestorCampo.Application.AuditLogs;
using GestorCampo.Application.AuditLogs.DTOs;
using GestorCampo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "SuperAdmin,Supervisor")]
public class AuditLogsController : ControllerBase
{
    private readonly AuditLogService _auditLogs;

    public AuditLogsController(AuditLogService auditLogs) => _auditLogs = auditLogs;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] AuditLogListRequest request, CancellationToken ct)
    {
        var result = await _auditLogs.GetListAsync(request, ct);
        return Ok(result.Data);
    }
}
