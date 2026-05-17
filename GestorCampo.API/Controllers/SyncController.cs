// GestorCampo.API/Controllers/SyncController.cs
using GestorCampo.Application.Common;
using GestorCampo.Application.Sync;
using GestorCampo.Application.Sync.DTOs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize(Roles = "SuperAdmin")]
public class SyncController : ControllerBase
{
    private readonly SyncService _sync;

    public SyncController(SyncService sync) => _sync = sync;

    [HttpGet("logs")]
    [ProducesResponseType(typeof(PagedResult<SyncLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs([FromQuery] SyncLogListRequest request, CancellationToken ct)
    {
        var result = await _sync.GetLogsAsync(request, ct);
        return Ok(result.Data);
    }

    [HttpPost("clients")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status202Accepted)]
    public IActionResult TriggerClientSync()
    {
        BackgroundJob.Enqueue<SyncService>(s => s.SyncClientsAsync(CancellationToken.None));
        return Accepted(new MessageResponse { Message = "Sync de clientes encolado" });
    }

    [HttpPost("products")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status202Accepted)]
    public IActionResult TriggerProductSync()
    {
        BackgroundJob.Enqueue<SyncService>(s => s.SyncProductsAsync(CancellationToken.None));
        return Accepted(new MessageResponse { Message = "Sync de productos encolado" });
    }
}
