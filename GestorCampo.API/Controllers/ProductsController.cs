using System.IdentityModel.Tokens.Jwt;
using GestorCampo.Application.Products;
using GestorCampo.Application.Products.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly ProductService _products;

    public ProductsController(ProductService products) => _products = products;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] ProductListRequest request, CancellationToken ct)
    {
        var result = await _products.GetListAsync(request, ct);
        if (!result.Succeeded) return StatusCode(500, new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _products.GetByIdAsync(id, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await _products.CreateAsync(request, CurrentUserId, ct);
        if (!result.Succeeded) return Conflict(new { error = result.Error });
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var result = await _products.UpdateAsync(id, request, CurrentUserId, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _products.DeleteAsync(id, CurrentUserId, ct);
        if (!result.Succeeded) return NotFound(new { error = result.Error });
        return NoContent();
    }
}
