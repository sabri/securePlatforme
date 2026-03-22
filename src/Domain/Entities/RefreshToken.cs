namespace SecurePlatform.Domain.Entities;

/// <summary>
/// Refresh token entity for JWT token rotation.
/// This is critical for secure logout & token revocation.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public string? ReasonRevoked { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Foreign key
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}
