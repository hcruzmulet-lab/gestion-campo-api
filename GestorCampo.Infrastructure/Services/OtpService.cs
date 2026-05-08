using System.Security.Cryptography;
using GestorCampo.Domain.Interfaces.Services;
using StackExchange.Redis;

namespace GestorCampo.Infrastructure.Services;

public class OtpService : IOtpService
{
    private readonly IDatabase _redis;

    public OtpService(IConnectionMultiplexer redis) =>
        _redis = redis.GetDatabase();

    public string GenerateCode() =>
        RandomNumberGenerator.GetInt32(100000, 999999).ToString();

    public async Task StoreAsync(Guid userId, string code, string purpose, int expiryMinutes, CancellationToken ct = default)
    {
        var key = $"otp:{userId}:{purpose}";
        await _redis.StringSetAsync(key, code, TimeSpan.FromMinutes(expiryMinutes));
    }

    public async Task<bool> ValidateAsync(Guid userId, string code, string purpose, CancellationToken ct = default)
    {
        var key = $"otp:{userId}:{purpose}";
        var stored = await _redis.StringGetAsync(key);
        if (stored.IsNullOrEmpty || stored != code)
            return false;
        await _redis.KeyDeleteAsync(key);
        return true;
    }
}
