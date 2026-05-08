namespace GestorCampo.Application.Auth.DTOs;
public record ResetPasswordRequest(string Token, string NewPassword);
