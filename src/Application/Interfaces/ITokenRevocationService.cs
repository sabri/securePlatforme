namespace SecurePlatform.Application.Interfaces;

/// <summary>
/// Manages JWT access token revocation via a blacklist.
/// Revoked token JTIs are stored until their natural expiry.
/// </summary>
public interface ITokenRevocationService
{
    /// <summary>Revoke a token by its JTI claim. TTL = remaining token lifetime.</summary>
    Task RevokeTokenAsync(string jti, TimeSpan remainingLifetime);

    /// <summary>Check whether a token JTI has been revoked.</summary>
    Task<bool> IsTokenRevokedAsync(string jti);
}
