using System.Security.Cryptography;
using GestorCampo.Domain.Interfaces.Services;

namespace GestorCampo.Infrastructure.Services;

public class PasswordService : IPasswordService
{
    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);

    public string GenerateSecureToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
}
