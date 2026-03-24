namespace SecurePlatform.Application.DTOs.Auth;

/// <summary>
/// Step 2: User submits the code they received + new password.
/// </summary>
public record ResetPasswordRequest(string Email, string Code, string NewPassword, string ConfirmPassword);
