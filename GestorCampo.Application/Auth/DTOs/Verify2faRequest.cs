namespace GestorCampo.Application.Auth.DTOs;
public record Verify2faRequest(Guid UserId, string Code);
