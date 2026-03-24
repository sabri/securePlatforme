using SecurePlatform.Application.Interfaces;
using StackExchange.Redis;

namespace SecurePlatform.Infrastructure.Services;

/// <summary>
/// Redis-backed token revocation service.
/// Stores revoked JTIs as keys with a TTL matching the token's remaining lifetime,
/// so entries auto-expire and don't accumulate forever.
/// </summary>
public class RedisTokenRevocationService : ITokenRevocationService
{
    private const string KeyPrefix = "revoked-token:";
    private readonly IConnectionMultiplexer _redis;

    public RedisTokenRevocationService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task RevokeTokenAsync(string jti, TimeSpan remainingLifetime)
    {
        if (remainingLifetime <= TimeSpan.Zero)
            return; // Token already expired — nothing to revoke.

        var db = _redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}{jti}", "revoked", remainingLifetime);
    }

    public async Task<bool> IsTokenRevokedAsync(string jti)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"{KeyPrefix}{jti}");
    }
}
