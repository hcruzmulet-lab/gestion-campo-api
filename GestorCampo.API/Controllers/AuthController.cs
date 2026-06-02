// GestorCampo.API/Controllers/AuthController.cs
using System.IdentityModel.Tokens.Jwt;
using GestorCampo.Application.Auth;
using GestorCampo.Application.Auth.DTOs;
using GestorCampo.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestorCampo.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request, ct);
        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });
        return Ok(result.Data);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        await _auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }

    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _auth.ForgotPasswordAsync(request, ct);
        return Ok(new MessageResponse { Message = "Si el email existe, recibirás instrucciones en breve" });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _auth.ResetPasswordAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });
        return Ok(new MessageResponse { Message = "Contraseña actualizada correctamente" });
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var currentUserId = Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var result = await _auth.ChangePasswordAsync(currentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });
        return Ok(new MessageResponse { Message = "Contraseña actualizada correctamente" });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _auth.VerifyEmailAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });
        return Ok(new MessageResponse { Message = "Email verificado correctamente" });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("2fa/send")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send2fa([FromBody] Send2faRequest request, CancellationToken ct)
    {
        var result = await _auth.Send2faAsync(request, ct);
        if (!result.Succeeded)
            return BadRequest(new { error = result.Error });
        return Ok(new MessageResponse { Message = "Código enviado a tu email" });
    }

    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Verify2fa([FromBody] Verify2faRequest request, CancellationToken ct)
    {
        var result = await _auth.Verify2faAsync(request, ct);
        if (!result.Succeeded)
            return Unauthorized(new { error = result.Error });
        return Ok(result.Data);
    }
}
