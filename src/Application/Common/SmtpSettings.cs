namespace SecurePlatform.Application.Common;

/// <summary>
/// SMTP settings — mapped from appsettings.json "SmtpSettings" section.
/// </summary>
public class SmtpSettings
{
    public const string SectionName = "SmtpSettings";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "SecurePlatform";
    public bool UseSsl { get; set; } = true;
}
