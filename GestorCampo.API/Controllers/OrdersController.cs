using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GestorCampo.Application.Common;
using GestorCampo.Application.Orders;
using GestorCampo.Application.Orders.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetList([FromQuery] OrderListRequest request, CancellationToken ct)
    {
        var result = await _orders.GetListAsync(request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderRequest request, CancellationToken ct)
    {
        var result = await _orders.UpdateAsync(id, request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            if (result.Error.Contains("al menos") || result.Error.Contains("línea"))
                return BadRequest(new { error = result.Error });
            if (result.Error.Contains("no encontrado") || result.Error.Contains("no encontrada"))
                return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/send")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
    {
        var result = await _orders.SendAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await _orders.ApproveAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SuperAdmin,Supervisor")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectOrderRequest request, CancellationToken ct)
    {
        var result = await _orders.RejectAsync(id, request, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _orders.DeleteAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            if (result.Error.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(BulkDeleteResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> BulkDelete([FromBody] BulkDeleteOrdersRequest request, CancellationToken ct)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
            return BadRequest(new { error = "Debe enviar al menos un id" });
        var result = await _orders.BulkDeleteAsync(request.Ids, CurrentUserId, CurrentRole, ct);
        return Ok(result.Data);
    }

    [HttpPost("{id:guid}/deliver")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken ct)
    {
        var result = await _orders.DeliverAsync(id, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return Conflict(new { error = result.Error });
        }
        return Ok(result.Data);
    }
}
