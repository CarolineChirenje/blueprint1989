using Divvy.Api.Data;
using Divvy.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Divvy.Api.Services;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHashingService passwordHashingService)
    {
        await SeedRolesAsync(context);
        await SeedNotificationTypesAsync(context);
        await SeedAppConfigAsync(context);
        await SeedUsersAsync(context, passwordHashingService);
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        if (!await context.RoleEntities.AnyAsync())
        {
            context.RoleEntities.AddRange(
                new RoleEntity { Id = 1, Name = "SuperAdmin", Description = "Super Administrator with full system access", IsActive = true, CreatedAt = DateTime.UtcNow },
                new RoleEntity { Id = 2, Name = "Admin",      Description = "Administrator with system management access", IsActive = true, CreatedAt = DateTime.UtcNow },
                new RoleEntity { Id = 3, Name = "Member",     Description = "Household member", IsActive = true, CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedNotificationTypesAsync(ApplicationDbContext context)
    {
        if (!await context.NotificationTypes.AnyAsync())
        {
            context.NotificationTypes.AddRange(
                new NotificationTypeEntity { Id = 1, Name = "General",             Description = "General or test notification" },
                new NotificationTypeEntity { Id = 2, Name = "PaymentDue",            Description = "A payment obligation is due" },
                new NotificationTypeEntity { Id = 3, Name = "PaymentReceived",       Description = "A payment has been recorded" },
                new NotificationTypeEntity { Id = 4, Name = "CycleCreated",          Description = "A new expense cycle has been created" },
                new NotificationTypeEntity { Id = 5, Name = "SystemRestart",         Description = "System is restarting" },
                new NotificationTypeEntity { Id = 6, Name = "GroupInviteReceived",   Description = "A user has been invited to join a group" }
            );
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedUsersAsync(ApplicationDbContext context, IPasswordHashingService passwordHashingService)
    {
        if (!await context.Users.AnyAsync())
        {
            var users = new List<User>
            {
                new User
                {
                    Email               = "carochire@gmail.com",
                    FirstName           = "Caroline",
                    LastName            = "Chirenje",
                    PasswordHash        = passwordHashingService.HashPassword("SuperAdmin123!"),
                    PasswordLastChanged = DateTime.UtcNow,
                    Role                = Role.SuperAdmin,
                    IsActive            = true,
                    IsMfaEnabled        = false,
                    IsEmailVerified     = true,
                    CreatedAt           = DateTime.UtcNow,
                    UpdatedAt           = DateTime.UtcNow
                },
                new User
                {
                    Email               = "admin@elroitec.com",
                    FirstName           = "Divvy",
                    LastName            = "Admin",
                    PasswordHash        = passwordHashingService.HashPassword("SuperAdmin123!"),
                    PasswordLastChanged = DateTime.UtcNow,
                    Role                = Role.Admin,
                    IsActive            = true,
                    IsMfaEnabled        = false,
                    IsEmailVerified     = true,
                    CreatedAt           = DateTime.UtcNow,
                    UpdatedAt           = DateTime.UtcNow
                }
            };

            context.Users.AddRange(users);
            await context.SaveChangesAsync();

            var typeIds = await context.NotificationTypes.Select(t => t.Id).ToListAsync();
            foreach (var user in users)
            {
                foreach (var typeId in typeIds)
                {
                    context.UserNotificationPreferences.Add(new UserNotificationPreference
                    {
                        UserId             = user.Id,
                        NotificationTypeId = typeId,
                        IsEnabled          = true
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedAppConfigAsync(ApplicationDbContext context)
    {
        var defaults = new List<AppConfigEntry>
        {
            // System
            new() { Key = AppConfigKeys.AppName,         Value = "Divvy",                       DataType = "string", Category = "System", DisplayName = "Application Name",                Description = "Display name of the application.",                                                  IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.FrontendVersion, Value = "1.0.0",                        DataType = "string", Category = "System", DisplayName = "Frontend Version",                Description = "Current Angular client version.",                                                   IsReadOnly = false, RequiresRestart = true  },
            new() { Key = AppConfigKeys.BackendVersion,  Value = "1.0.0",                        DataType = "string", Category = "System", DisplayName = "Backend Version",                 Description = "Current .NET API version.",                                                         IsReadOnly = false, RequiresRestart = true  },
            new() { Key = AppConfigKeys.DefaultTimeZone, Value = "AUS Eastern Standard Time",    DataType = "string", Category = "System", DisplayName = "Default Time Zone",               Description = "Windows time-zone ID used for date/time display.",                                  IsReadOnly = false, RequiresRestart = true  },
            new() { Key = AppConfigKeys.AppDomain,       Value = "https://divvy.elroitec.com",   DataType = "string", Category = "System", DisplayName = "Application Domain",              Description = "Base URL of the frontend. Used for email links and URL generation.",               IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.DocsUrl,         Value = "https://divvy-docs.pages.dev", DataType = "string", Category = "System", DisplayName = "Technical Documentation URL",     Description = "URL of the hosted technical documentation site.",                                   IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SystemRestartNoticeDelaySeconds, Value = "10",            DataType = "int",    Category = "System", DisplayName = "Restart Notice Delay (seconds)",  Description = "Seconds to wait after sending the restart notification before stopping.",          IsReadOnly = false, RequiresRestart = false },

            // Authentication
            new() { Key = AppConfigKeys.PasswordExpirationDays,            Value = "30",               DataType = "int",    Category = "Authentication", DisplayName = "Password Expiration (days)",               Description = "Days before a password must be changed.",                        IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.JwtExpirationMinutes,              Value = "120",              DataType = "int",    Category = "Authentication", DisplayName = "JWT Token Expiry (minutes)",               Description = "How long a JWT access token remains valid.",                     IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.AdminSignupPin,                    Value = "DIVVY-ADMIN-2026", DataType = "string", Category = "Authentication", DisplayName = "Administrator Signup PIN",                 Description = "PIN required when a new Admin account self-registers.",          IsReadOnly = false, RequiresRestart = false, IsSecret = true },
            new() { Key = AppConfigKeys.PasswordResetTokenValidityMinutes, Value = "15",               DataType = "int",    Category = "Authentication", DisplayName = "Password Reset Token Validity (minutes)",  Description = "How long a password reset token remains valid.",                  IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.PasswordResetRequestLimitPerHour,  Value = "3",                DataType = "int",    Category = "Authentication", DisplayName = "Password Reset Requests Per Hour",         Description = "Max password reset requests per email per hour.",                IsReadOnly = false, RequiresRestart = false },

            // Email / SMTP
            new() { Key = AppConfigKeys.SmtpHost,                            Value = "smtp.gmail.com",                DataType = "string", Category = "Email", DisplayName = "SMTP Host",                               Description = "SMTP server hostname.",                                          IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpPort,                            Value = "587",                           DataType = "int",    Category = "Email", DisplayName = "SMTP Port",                               Description = "SMTP server port.",                                              IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpUsername,                        Value = "elroitec@gmail.com",            DataType = "string", Category = "Email", DisplayName = "SMTP Username",                           Description = "Email address for SMTP authentication.",                         IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpPassword,                        Value = "",                              DataType = "string", Category = "Email", DisplayName = "SMTP Password",                           Description = "Password for SMTP authentication. KEEP CONFIDENTIAL.",          IsReadOnly = false, RequiresRestart = false, IsSecret = true },
            new() { Key = AppConfigKeys.SmtpFromEmail,                       Value = "noreply@elroitec.com",          DataType = "string", Category = "Email", DisplayName = "SMTP From Email",                         Description = "Sender email address.",                                          IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpFromName,                        Value = "Divvy",                         DataType = "string", Category = "Email", DisplayName = "SMTP From Name",                          Description = "Sender display name for automated emails.",                      IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ForgotPasswordResetUrlPath,          Value = "/reset-password?token={token}", DataType = "string", Category = "Email", DisplayName = "Password Reset URL Path",                 Description = "URL path for password reset. Must include {token}.",             IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailVerificationUrlPath,            Value = "/verify-email?token={token}",   DataType = "string", Category = "Email", DisplayName = "Email Verification URL Path",             Description = "URL path for email verification. Must include {token}.",         IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailVerificationTokenValidityHours, Value = "24",                            DataType = "int",    Category = "Email", DisplayName = "Email Verification Token Validity (hours)", Description = "How long an email verification token remains valid.",            IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailVerificationResendLimitPerHour, Value = "3",                             DataType = "int",    Category = "Email", DisplayName = "Email Verification Resend Limit Per Hour", Description = "Max resend requests per email per hour.",                        IsReadOnly = false, RequiresRestart = false },

            // Ledger
            new() { Key = AppConfigKeys.MembersPerCycle,             Value = "4", DataType = "int", Category = "Ledger", DisplayName = "Members Per Cycle",               Description = "Default number of members expected to contribute per expense cycle.", IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.PaymentReminderIntervalDays, Value = "3", DataType = "int", Category = "Ledger", DisplayName = "Payment Reminder Interval (days)", Description = "Days between automated payment-due reminder notifications.",         IsReadOnly = false, RequiresRestart = false },
        };

        var existingKeys = await context.AppConfigEntries.Select(e => e.Key).ToHashSetAsync();

        var now = DateTime.UtcNow;
        var toInsert = defaults
            .Where(d => !existingKeys.Contains(d.Key))
            .Select(d => { d.CreatedAt = now; d.UpdatedAt = now; return d; })
            .ToList();

        if (toInsert.Count > 0)
        {
            context.AppConfigEntries.AddRange(toInsert);
            await context.SaveChangesAsync();
        }

        var secretKeys    = defaults.Where(d => d.IsSecret).Select(d => d.Key).ToHashSet();
        var nonSecretKeys = defaults.Where(d => !d.IsSecret).Select(d => d.Key).ToHashSet();
        var existingEntries = await context.AppConfigEntries.ToListAsync();

        bool metaChanged = false;
        foreach (var entry in existingEntries)
        {
            if (secretKeys.Contains(entry.Key) && !entry.IsSecret)        { entry.IsSecret = true;  metaChanged = true; }
            else if (nonSecretKeys.Contains(entry.Key) && entry.IsSecret)  { entry.IsSecret = false; metaChanged = true; }
        }

        if (metaChanged) await context.SaveChangesAsync();
    }
}
