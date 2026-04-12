using Microsoft.Extensions.Logging;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.Infrastructure.Services;

/// <summary>
/// Development email service — logs the reset code to the console.
/// Replace with a real SMTP/Brevo/SendGrid implementation in production.
/// </summary>
public class DevEmailService : IEmailService
{
    private readonly ILogger<DevEmailService> _logger;

    public DevEmailService(ILogger<DevEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetCodeAsync(string toEmail, string resetCode)
    {
        _logger.LogWarning(
            "═══════════════════════════════════════════\n" +
            "  PASSWORD RESET CODE for {Email}\n" +
            "  Code: {Code}\n" +
            "═══════════════════════════════════════════",
            toEmail, resetCode);

        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string toEmail, string confirmationToken)
    {
        _logger.LogWarning(
            "═══════════════════════════════════════════\n" +
            "  EMAIL CONFIRMATION for {Email}\n" +
            "  Token: {Token}\n" +
            "═══════════════════════════════════════════",
            toEmail, confirmationToken);

        return Task.CompletedTask;
    }
}
