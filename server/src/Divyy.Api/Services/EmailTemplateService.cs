using Divvy.Api.Models;

namespace Divvy.Api.Services;

/// <summary>
/// Builds HTML and plain-text bodies for all authentication-related emails.
/// All branding values are read from AppConfig so no strings are hardcoded.
/// </summary>
public class EmailTemplateService
{
    private readonly AppConfigService _appConfig;

    public EmailTemplateService(AppConfigService appConfig)
    {
        _appConfig = appConfig;
    }

    // ── Shared helpers ────────────────────────────────────────────────────────

    private async Task<(string appName, string appDisplayName, string loginUrl)> GetBrandingAsync()
    {
        var appName        = await _appConfig.GetStringAsync(AppConfigKeys.AppName, "App");
        var appDisplayName = appName + " Health";
        var domain         = await _appConfig.GetStringAsync(AppConfigKeys.AppDomain, "");
        var loginUrl       = string.IsNullOrWhiteSpace(domain) ? "" : domain.TrimEnd('/') + "/login";
        return (appName, appDisplayName, loginUrl);
    }

    /// <summary>Wraps body content in a consistent mobile-friendly HTML shell.</summary>
    private static string BuildHtmlLayout(string title, string bodyContent, string appDisplayName)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
  <meta charset=""utf-8"" />
  <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
  <title>{title}</title>
  <style>
    body {{ margin: 0; padding: 0; background-color: #f4f4f7; font-family: Arial, sans-serif; color: #333333; }}
    .wrapper {{ width: 100%; max-width: 600px; margin: 32px auto; background: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 2px 8px rgba(0,0,0,0.08); }}
    .header {{ background-color: #1a1a2e; padding: 24px 32px; }}
    .header h1 {{ margin: 0; font-size: 20px; color: #ffffff; font-weight: 600; letter-spacing: 0.5px; }}
    .content {{ padding: 32px; line-height: 1.7; font-size: 15px; }}
    .content p {{ margin: 0 0 16px 0; }}
    .cta-wrapper {{ margin: 24px 0; }}
    .cta-button {{ display: inline-block; background-color: #1a1a2e; color: #ffffff !important; padding: 13px 28px; text-decoration: none; border-radius: 6px; font-size: 15px; font-weight: 600; }}
    .fallback {{ font-size: 12px; color: #888888; word-break: break-all; margin-top: 8px; }}
    .security-note {{ background-color: #fff8e1; border-left: 4px solid #f0a500; padding: 12px 16px; border-radius: 4px; font-size: 13px; margin: 20px 0 0 0; }}
    .footer {{ border-top: 1px solid #eeeeee; padding: 20px 32px; font-size: 12px; color: #999999; text-align: center; }}
  </style>
</head>
<body>
  <div class=""wrapper"">
    <div class=""header""><h1>{title}</h1></div>
    <div class=""content"">
{bodyContent}
    </div>
    <div class=""footer"">
      <p>{appDisplayName} &mdash; Keeping your account secure</p>
    </div>
  </div>
</body>
</html>";
    }

    // ── Email 1: Verification (Signup) ─────────────────────────────────────────

    /// <summary>
    /// Builds the email verification email sent after signup.
    /// </summary>
    public async Task<(string html, string plain)> BuildVerificationEmailAsync(
        string firstName, string verificationUrl, int validityHours)
    {
        var (appName, appDisplayName, _) = await GetBrandingAsync();

        var bodyHtml = $@"      <p>Hi {firstName},</p>
      <p>Welcome to <strong>{appName}</strong>. To get started, please confirm your email address by clicking the button below.</p>
      <div class=""cta-wrapper"">
        <a href=""{verificationUrl}"" class=""cta-button"">Verify Email</a>
      </div>
      <p class=""fallback"">If the button doesn&apos;t work, copy and paste this link into your browser:<br />{verificationUrl}</p>
      <p>This link will expire in <strong>{validityHours} hours</strong>.</p>
      <div class=""security-note"">If you did not create a {appName} account, you can safely ignore this email.</div>";

        var html  = BuildHtmlLayout($"Welcome to {appName}", bodyHtml, appDisplayName);

        var plain = $@"Welcome to {appName}

Hi {firstName},

To get started, please confirm your email address by visiting the link below:

{verificationUrl}

This link will expire in {validityHours} hours.

If you did not create a {appName} account, you can safely ignore this email.

—
{appDisplayName}
Keeping your account secure";

        return (html, plain);
    }

    // ── Email 2: Password Reset Request ────────────────────────────────────────

    /// <summary>
    /// Builds the password reset request email.
    /// </summary>
    public async Task<(string html, string plain)> BuildPasswordResetRequestEmailAsync(
        string firstName, string resetUrl, int validityMinutes)
    {
        var (appName, appDisplayName, _) = await GetBrandingAsync();

        var bodyHtml = $@"      <p>Hi {firstName},</p>
      <p>We received a request to reset the password for your <strong>{appName}</strong> account. If this was you, click the button below to continue.</p>
      <div class=""cta-wrapper"">
        <a href=""{resetUrl}"" class=""cta-button"">Reset Password</a>
      </div>
      <p class=""fallback"">If the button doesn&apos;t work, copy and paste this link into your browser:<br />{resetUrl}</p>
      <p>This link will expire in <strong>{validityMinutes} minutes</strong> for security reasons.</p>
      <div class=""security-note"">If you did not request a password reset, you can safely ignore this email. Your account password remains unchanged.</div>";

        var html  = BuildHtmlLayout($"Reset your {appName} password", bodyHtml, appDisplayName);

        var plain = $@"Password Reset Request

Hi {firstName},

We received a request to reset the password for your {appName} account.

If this was you, use the link below to reset your password:

{resetUrl}

This link will expire in {validityMinutes} minutes for security reasons.

If you did not request a password reset, you can safely ignore this email. Your account password remains unchanged.

—
{appDisplayName}
Keeping your account secure";

        return (html, plain);
    }

    // ── Email 3: Password Reset Successful ─────────────────────────────────────

    /// <summary>
    /// Builds the password reset confirmation email sent after a successful reset.
    /// </summary>
    public async Task<(string html, string plain)> BuildPasswordResetSuccessEmailAsync(
        string firstName)
    {
        var (appName, appDisplayName, loginUrl) = await GetBrandingAsync();

        var loginButtonHtml = string.IsNullOrWhiteSpace(loginUrl)
            ? ""
            : $@"
      <div class=""cta-wrapper"">
        <a href=""{loginUrl}"" class=""cta-button"">Log In</a>
      </div>";

        var bodyHtml = $@"      <p>Hi {firstName},</p>
      <p>Your <strong>{appName}</strong> account password has been successfully updated. You can now log in with your new password.{loginButtonHtml}</p>
      <div class=""security-note"">If you did not make this change, please contact your system administrator immediately as your account security may be at risk.</div>";

        var html  = BuildHtmlLayout($"Your {appName} password has been updated", bodyHtml, appDisplayName);

        var loginLine = string.IsNullOrWhiteSpace(loginUrl)
            ? ""
            : $"\nYou can log in here: {loginUrl}\n";

        var plain = $@"Password Reset Successful

Hi {firstName},

Your {appName} account password has been successfully updated. You can now log in with your new password.
{loginLine}
If you did not make this change, please contact your system administrator immediately as your account security may be at risk.

—
{appDisplayName}
Security notification";

        return (html, plain);
    }
}
