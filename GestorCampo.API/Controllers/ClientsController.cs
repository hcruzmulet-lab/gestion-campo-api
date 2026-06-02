using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Clients;
using GestorCampo.Application.Clients.DTOs;
using GestorCampo.Application.Common;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        Enum.Parse<UserRole>(User.FindFirst("role")!.Value);

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClientResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetList([FromQuery] ClientListRequest request, CancellationToken ct)
    {
        var result = await _clients.GetListAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateClientRequest request, CancellationToken ct)
    {
        var result = await _clients.CreateAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("vendedor")) return BadRequest(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        var result = await _clients.UpdateAsync(id, request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("vendedor")) return BadRequest(new { error = result.Error });
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _clients.DeleteAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }
}
