using SecurePlatform.Application.DTOs.Auth;

namespace SecurePlatform.Application.Interfaces;

/// <summary>
/// Core authentication service interface.
/// Implemented in the Infrastructure layer.
/// </summary>
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<bool> LogoutAsync(string userId);
    Task<UserDto?> GetCurrentUserAsync(string userId);
}
