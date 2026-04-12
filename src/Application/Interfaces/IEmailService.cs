namespace SecurePlatform.Application.Interfaces;

public interface IEmailService
{
    Task SendPasswordResetCodeAsync(string toEmail, string resetCode);
    Task SendEmailConfirmationAsync(string toEmail, string confirmationToken);
}
