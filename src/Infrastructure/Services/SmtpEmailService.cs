using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SecurePlatform.Application.Common;
using SecurePlatform.Application.Interfaces;

namespace SecurePlatform.Infrastructure.Services;

/// <summary>
/// Real SMTP email service using System.Net.Mail.
/// Works with any SMTP provider: Gmail, Outlook, Brevo, SendGrid, Mailjet, etc.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<SmtpSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetCodeAsync(string toEmail, string resetCode)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = "SecurePlatform — Password Reset Code",
            IsBodyHtml = true,
            Body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 2rem;">
                    <h2 style="color: #4f46e5;">Password Reset</h2>
                    <p>You requested a password reset for your SecurePlatform account.</p>
                    <p>Your reset code is:</p>
                    <div style="background: #f0f2f5; padding: 1rem; border-radius: 8px; text-align: center; font-size: 1.5rem; font-weight: bold; letter-spacing: 2px; margin: 1rem 0;">
                        {resetCode}
                    </div>
                    <p style="color: #666; font-size: 0.9rem;">If you did not request this, you can safely ignore this email.</p>
                </div>
                """
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            EnableSsl = _settings.UseSsl
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Password reset email sent to {Email}", toEmail);
    }

    public async Task SendEmailConfirmationAsync(string toEmail, string confirmationToken)
    {
        var message = new MailMessage
        {
            From = new MailAddress(_settings.FromEmail, _settings.FromName),
            Subject = "SecurePlatform — Confirm Your Email",
            IsBodyHtml = true,
            Body = $"""
                <div style="font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 2rem;">
                    <h2 style="color: #4f46e5;">Email Confirmation</h2>
                    <p>Welcome to SecurePlatform! Please confirm your email address.</p>
                    <p>Your confirmation code is:</p>
                    <div style="background: #f0f2f5; padding: 1rem; border-radius: 8px; text-align: center; font-size: 1.5rem; font-weight: bold; letter-spacing: 2px; margin: 1rem 0;">
                        {confirmationToken}
                    </div>
                    <p style="color: #666; font-size: 0.9rem;">If you did not create an account, you can safely ignore this email.</p>
                </div>
                """
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            EnableSsl = _settings.UseSsl
        };

        await client.SendMailAsync(message);
        _logger.LogInformation("Email confirmation sent to {Email}", toEmail);
    }
}
