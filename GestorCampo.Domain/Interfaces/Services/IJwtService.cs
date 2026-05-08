using GestorCampo.Domain.Entities;

namespace GestorCampo.Domain.Interfaces.Services;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    (string token, string tokenHash) GenerateRefreshToken();
}
