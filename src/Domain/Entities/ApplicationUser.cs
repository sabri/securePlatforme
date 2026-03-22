using Microsoft.AspNetCore.Identity;

namespace SecurePlatform.Domain.Entities;

/// <summary>
/// Custom user entity extending ASP.NET Core Identity.
/// Add your own properties here as the app grows.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties for future expansion
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
