using System.IdentityModel.Tokens.Jwt;
using GestorCampo.Application.Visits;
using GestorCampo.Application.Visits.DTOs;
using GestorCampo.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestorCampo.API.Controllers;

[ApiController]
[Authorize]
[Route("api/visits/{visitId:guid}/attachments")]
public class VisitAttachmentsController : ControllerBase
{
    private readonly VisitAttachmentService _svc;
    public VisitAttachmentsController(VisitAttachmentService svc) => _svc = svc;

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    private UserRole CurrentRole =>
        Enum.Parse<UserRole>(User.FindFirst("role")!.Value);

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]   // 10 MB request cap
    public async Task<IActionResult> Upload(Guid visitId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Archivo vacío" });
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Archivo excede 5 MB" });

        await using var stream = file.OpenReadStream();
        var result = await _svc.UploadAsync(visitId,
            new UploadAttachmentRequest
            {
                Content = stream,
                ContentType = file.ContentType ?? "image/jpeg",
                SizeBytes = file.Length
            },
            CurrentUserId, CurrentRole, ct);

        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            if (result.Error.Contains("no encontrada")) return NotFound(new { error = result.Error });
            return BadRequest(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid visitId, CancellationToken ct)
    {
        var result = await _svc.ListAsync(visitId, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            return NotFound(new { error = result.Error });
        }
        return Ok(result.Data);
    }

    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid visitId, Guid attachmentId, CancellationToken ct)
    {
        var result = await _svc.DeleteAsync(visitId, attachmentId, CurrentUserId, CurrentRole, ct);
        if (!result.Succeeded)
        {
            if (result.Error!.Contains("acceso")) return Forbid();
            return NotFound(new { error = result.Error });
        }
        return NoContent();
    }
}
