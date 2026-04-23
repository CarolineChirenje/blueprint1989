namespace Blueprint1989.Api.Services;

/// <summary>
/// Service for sending emails via SMTP.
/// Configuration is read from AppConfigEntry at runtime, allowing admins to change settings without restart.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email to the specified recipient.
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject line</param>
    /// <param name="htmlBody">Email body in HTML format</param>
    /// <param name="plainBody">Optional plaintext fallback for the email body</param>
    /// <returns>True if email sent successfully, false if SMTP not configured or error occurred</returns>
    Task<bool> SendEmailAsync(string to, string subject, string htmlBody, string? plainBody = null);
}
