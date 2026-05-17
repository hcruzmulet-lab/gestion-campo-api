using GestorCampo.Application.Dashboard;
using GestorCampo.Application.Dashboard.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "SuperAdmin,Supervisor")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboard;
    private readonly AgentStatusService _agentStatus;

    public DashboardController(DashboardService dashboard, AgentStatusService agentStatus)
    {
        _dashboard = dashboard;
        _agentStatus = agentStatus;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(DashboardStatsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _dashboard.GetStatsAsync(ct);
        return Ok(result.Data);
    }

    [HttpGet("agents-status")]
    [ProducesResponseType(typeof(List<AgentStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgentsStatus(CancellationToken ct)
    {
        var result = await _agentStatus.GetAgentStatusesAsync(ct);
        return Ok(result);
    }
}
