using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Users;
using GestorCampo.Application.Users.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _users;

    public UsersController(UserService users) => _users = users;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private UserRole CurrentRole =>
        Enum.Parse<UserRole>(User.FindFirst("role")!.Value);

    [HttpGet]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> GetList([FromQuery] UserListRequest request, CancellationToken ct)
    {
        var result = await _users.GetListAsync(request, CurrentUserId, CurrentRole, ct);
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _users.GetByIdAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _users.CreateAsync(request, CurrentUserId, ct);
        if (!result.Succeeded) return Conflict(new { error = result.Error });
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _users.UpdateAsync(id, request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error == "Usuario no encontrado") return NotFound(new { error = result.Error });
            return StatusCode(403, new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _users.DeleteAsync(id, CurrentUserId, ct);
        if (!result.Succeeded) return BadRequest(new { error = result.Error });
        return NoContent();
    }

    [HttpPost("{id:guid}/block")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct)
    {
        var result = await _users.BlockAsync(id, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }

    [HttpPost("{id:guid}/unblock")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct)
    {
        var result = await _users.UnblockAsync(id, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }

    [HttpPost("{id:guid}/toggle-2fa")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Toggle2Fa(Guid id, CancellationToken ct)
    {
        var result = await _users.Toggle2FaAsync(id, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }
}
