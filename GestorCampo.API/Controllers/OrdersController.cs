using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Orders;
using GestorCampo.Application.Orders.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orders;

    public OrdersController(OrderService orders) => _orders = orders;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private UserRole CurrentRole =>
        Enum.Parse<UserRole>(User.FindFirst("role")!.Value);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] OrderListRequest request, CancellationToken ct)
    {
        var result = await _orders.GetListAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _orders.GetByIdAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        var result = await _orders.CreateAsync(request, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("al menos")) return BadRequest(new { error = result.Error });
            if (result.Error.Contains("no encontrado") || result.Error.Contains("no encontrada"))
                return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
    {
        var result = await _orders.SendAsync(id, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _orders.ApproveAsync(id, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectOrderRequest request, CancellationToken ct)
    {
        var result = await _orders.RejectAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/deliver")]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        var result = await _orders.DeliverAsync(id, CurrentUserId, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }
}
