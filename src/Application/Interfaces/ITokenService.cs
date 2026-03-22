using System.Security.Claims;

namespace SecurePlatform.Application.Interfaces;

/// <summary>
/// JWT token generation & validation interface.
/// Separating token logic makes it easy to swap implementations.
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
