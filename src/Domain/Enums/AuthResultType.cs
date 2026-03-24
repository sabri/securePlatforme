namespace SecurePlatform.Domain.Enums;

public enum AuthResultType
{
    Success,
    InvalidCredentials,
    UserNotFound,
    UserLocked,
    EmailNotConfirmed,
    TokenExpired,
    TokenInvalid,
    RegistrationFailed,
    DuplicateEmail,
    PasswordResetFailed,
    PasswordResetCodeSent,
    PasswordResetSuccess,
    ServerError
}
