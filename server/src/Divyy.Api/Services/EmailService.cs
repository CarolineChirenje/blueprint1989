using System.Net;
using System.Net.Mail;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

/// <summary>
/// Sends emails via SMTP configured in AppConfigEntry.
/// Reads all SMTP settings from the database at runtime.
/// </summary>
public class EmailService : IEmailService
{
    private readonly AppConfigService _appConfigService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(AppConfigService appConfigService, ILogger<EmailService> logger)
    {
        _appConfigService = appConfigService;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string htmlBody, string? plainBody = null)
    {
        try
        {
            // Read SMTP configuration from AppConfig
            var smtpHost = await _appConfigService.GetStringAsync(AppConfigKeys.SmtpHost, "");
            var smtpPortStr = await _appConfigService.GetStringAsync(AppConfigKeys.SmtpPort, "");
            var smtpUsername = await _appConfigService.GetStringAsync(AppConfigKeys.SmtpUsername, "");
            var smtpPassword = await _appConfigService.GetStringAsync(AppConfigKeys.SmtpPassword, "");
            var fromEmail = await _appConfigService.GetStringAsync(AppConfigKeys.SmtpFromEmail, "");
            var fromName = await _appConfigService.GetStringAsync(AppConfigKeys.SmtpFromName, "");

            // Validate required SMTP configuration
            if (string.IsNullOrWhiteSpace(smtpHost) || 
                string.IsNullOrWhiteSpace(smtpPortStr) ||
                string.IsNullOrWhiteSpace(smtpUsername) || 
                string.IsNullOrWhiteSpace(smtpPassword) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning(
                    "SMTP not fully configured. Cannot send email to {Recipient}. " +
                    "Please configure SMTP settings via app-config-management.",
                    to);
                return false;
            }

            // Parse SMTP port
            if (!int.TryParse(smtpPortStr, out int smtpPort))
            {
                _logger.LogWarning("Invalid SMTP port configured: {SmtpPort}", smtpPortStr);
                return false;
            }

            // Create SMTP client
            using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
            {
                smtpClient.EnableSsl = true;
                smtpClient.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                smtpClient.Timeout = 10000; // 10 second timeout

                // Create mail message
                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(fromEmail, fromName);
                    mailMessage.To.Add(to);
                    mailMessage.Subject = subject;
                    mailMessage.IsBodyHtml = true;
                    mailMessage.Body = htmlBody;

                    // Add plaintext alternative if provided
                    if (!string.IsNullOrWhiteSpace(plainBody))
                    {
                        mailMessage.AlternateViews.Add(
                            AlternateView.CreateAlternateViewFromString(plainBody, null, "text/plain"));
                    }

                    // Send email
                    await smtpClient.SendMailAsync(mailMessage);
                    _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'", to, subject);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient} with subject '{Subject}'", to, subject);
            return false;
        }
    }
}
