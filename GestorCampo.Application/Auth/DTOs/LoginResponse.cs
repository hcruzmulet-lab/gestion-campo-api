namespace GestorCampo.Application.Auth.DTOs;
public record LoginResponse(string AccessToken, string RefreshToken, bool Requires2fa, Guid UserId);
