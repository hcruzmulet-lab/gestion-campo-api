// GestorCampo.API/Controllers/SyncController.cs
using GestorCampo.Application.Sync;
using GestorCampo.Application.Sync.DTOs;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
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
    public async Task<IActionResult> GetLogs([FromQuery] SyncLogListRequest request, CancellationToken ct)
    {
        var result = await _sync.GetLogsAsync(request, ct);
        return Ok(result.Data);
    }

    [HttpPost("clients")]
    public IActionResult TriggerClientSync()
    {
        BackgroundJob.Enqueue<SyncService>(s => s.SyncClientsAsync(CancellationToken.None));
        return Accepted(new { message = "Sync de clientes encolado" });
    }

    [HttpPost("products")]
    public IActionResult TriggerProductSync()
    {
        BackgroundJob.Enqueue<SyncService>(s => s.SyncProductsAsync(CancellationToken.None));
        return Accepted(new { message = "Sync de productos encolado" });
    }
}
