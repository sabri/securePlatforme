namespace SecurePlatform.Application.DTOs.Auth;

/// <summary>
/// Step 1: User submits email to request a password reset code.
/// </summary>
public record ForgotPasswordRequest(string Email);
