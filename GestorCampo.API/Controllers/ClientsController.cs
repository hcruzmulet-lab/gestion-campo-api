using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Clients;
using GestorCampo.Application.Clients.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly ClientService _clients;

    public ClientsController(ClientService clients) => _clients = clients;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private UserRole CurrentRole =>
        Enum.Parse<UserRole>(User.FindFirst(ClaimTypes.Role)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ClientListRequest request, CancellationToken ct)
    {
        var result = await _clients.GetListAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _clients.GetByIdAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var result = await _clients.CreateAsync(request, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("vendedor")) return BadRequest(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        var result = await _clients.UpdateAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("vendedor")) return BadRequest(new { error = result.Error });
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _clients.DeleteAsync(id, CurrentUserId, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }
}
