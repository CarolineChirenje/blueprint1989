namespace Batanai.Api.Models;

/// <summary>
/// Strongly-typed constants for all AppConfig keys.
/// Always use these constants instead of inline strings to avoid magic-string bugs.
/// </summary>
public static class AppConfigKeys
{
    // -- System ----------------------------------------------------------------
    public const string AppName             = "AppName";
    public const string FrontendVersion     = "FrontendVersion";
    public const string BackendVersion      = "BackendVersion";
    public const string DefaultTimeZone     = "DefaultTimeZone";
    public const string AppDomain           = "AppDomain";

    /// <summary>Seconds between sending the restart push notification and actually stopping the process (default 10).</summary>
    public const string SystemRestartNoticeDelaySeconds = "SystemRestartNoticeDelaySeconds";

    // -- Authentication --------------------------------------------------------
    public const string PasswordExpirationDays  = "PasswordExpirationDays";
    public const string JwtExpirationMinutes    = "JwtExpirationMinutes";
    public const string AdminSignupPin          = "AdminSignupPin";
    public const string PasswordResetTokenValidityMinutes = "PasswordResetTokenValidityMinutes";
    public const string PasswordResetRequestLimitPerHour = "PasswordResetRequestLimitPerHour";

    // -- Email / SMTP ----------------------------------------------------------
    public const string SmtpHost                        = "SmtpHost";
    public const string SmtpPort                        = "SmtpPort";
    public const string SmtpUsername                    = "SmtpUsername";
    public const string SmtpPassword                    = "SmtpPassword";
    public const string SmtpFromEmail                   = "SmtpFromEmail";
    public const string SmtpFromName                    = "SmtpFromName";
    public const string ForgotPasswordResetUrlPath       = "ForgotPasswordResetUrlPath";
    public const string EmailVerificationUrlPath             = "EmailVerificationUrlPath";
    public const string EmailVerificationTokenValidityHours  = "EmailVerificationTokenValidityHours";
    public const string EmailVerificationResendLimitPerHour  = "EmailVerificationResendLimitPerHour";

    // -- Documentation ---------------------------------------------------------
    public const string DocsUrl = "DocsUrl";

    // -- Ledger ----------------------------------------------------------------
    public const string MembersPerCycle              = "MembersPerCycle";
    public const string PaymentReminderIntervalDays  = "PaymentReminderIntervalDays";
}
