using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Tracking;
using GestorCampo.Application.Tracking.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/tracking")]
[Authorize]
public class TrackingController : ControllerBase
{
    private readonly TrackingService _tracking;

    public TrackingController(TrackingService tracking) => _tracking = tracking;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpPost]
    [Authorize(Roles = "Vendor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddPoints([FromBody] BulkTrackingRequest request, CancellationToken ct)
    {
        await _tracking.AddPointsAsync(request, CurrentUserId, ct);
        return NoContent();
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    [ProducesResponseType(typeof(List<TrackingPointResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTrail([FromQuery] TrackingQueryRequest request, CancellationToken ct)
    {
        var result = await _tracking.GetTrailAsync(request, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }
}
