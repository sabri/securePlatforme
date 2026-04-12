using SecurePlatform.Application.DTOs.Auth;

namespace SecurePlatform.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
    Task<bool> LogoutAsync(string userId, string? accessTokenJti = null);
    Task<UserDto?> GetCurrentUserAsync(string userId);

    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);

    Task<AuthResponse> ExternalLoginAsync(string provider, string email, string firstName, string lastName);

    Task<AuthResponse> ConfirmEmailAsync(string email, string token);
    Task<AuthResponse> ResendConfirmationEmailAsync(string email);
}
