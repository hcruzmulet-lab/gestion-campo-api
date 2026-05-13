using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Visits;
using GestorCampo.Application.Visits.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/visits")]
[Authorize]
public class VisitsController : ControllerBase
{
    private readonly VisitService _visits;

    public VisitsController(VisitService visits) => _visits = visits;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private UserRole CurrentRole =>
        Enum.Parse<UserRole>(User.FindFirst("role")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] VisitListRequest request, CancellationToken ct)
    {
        var result = await _visits.GetListAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _visits.GetByIdAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVisitRequest request, CancellationToken ct)
    {
        var result = await _visits.CreateAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrado") || result.Error.Contains("no encontrada"))
                return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}/checkin")]
    public async Task<IActionResult> CheckIn(Guid id, [FromBody] CheckInRequest request, CancellationToken ct)
    {
        var result = await _visits.CheckInAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            if (result.Error.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPut("{id:guid}/checkout")]
    public async Task<IActionResult> CheckOut(Guid id, [FromBody] CheckOutRequest request, CancellationToken ct)
    {
        var result = await _visits.CheckOutAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            if (result.Error.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _visits.DeleteAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }
}
