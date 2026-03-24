namespace SecurePlatform.Application.Interfaces;

/// <summary>
/// Email sending abstraction. Swap implementations for dev (console) vs production (SMTP/Brevo).
/// </summary>
public interface IEmailService
{
    Task SendPasswordResetCodeAsync(string toEmail, string resetCode);
}
