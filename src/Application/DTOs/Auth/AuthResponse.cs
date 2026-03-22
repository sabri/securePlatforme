using SecurePlatform.Domain.Enums;

namespace SecurePlatform.Application.DTOs.Auth;

public record AuthResponse
{
    public bool Succeeded { get; init; }
    public AuthResultType ResultType { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? Message { get; init; }
    public UserDto? User { get; init; }

    public static AuthResponse Success(string accessToken, string refreshToken, DateTime expiresAt, UserDto user)
        => new()
        {
            Succeeded = true,
            ResultType = AuthResultType.Success,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = user
        };

    public static AuthResponse Failure(AuthResultType type, string message)
        => new()
        {
            Succeeded = false,
            ResultType = type,
            Message = message
        };
}
