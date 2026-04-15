using Batanai.Api.Data;
using Batanai.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Batanai.Api.Services;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, IPasswordHashingService passwordHashingService)
    {
        await SeedRolesAsync(context);
        await SeedNotificationTypesAsync(context);
        await SeedCurrenciesAsync(context);
        await SeedAppConfigAsync(context);
        await SeedUsersAsync(context, passwordHashingService);
        await SeedUserNotificationPreferencesAsync(context);
    }

    private static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        if (!await context.RoleEntities.AnyAsync())
        {
            context.RoleEntities.AddRange(
                new RoleEntity { Id = 1, Name = "SuperAdmin", Description = "Super Administrator with full system access", IsActive = true, CreatedAt = DateTime.UtcNow },
                new RoleEntity { Id = 2, Name = "Administrator",      Description = "Administrator with system management access", IsActive = true, CreatedAt = DateTime.UtcNow },
                new RoleEntity { Id = 3, Name = "Member",     Description = "Group member", IsActive = true, CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedNotificationTypesAsync(ApplicationDbContext context)
    {
        // Mandatory = IsAdminControlled: users cannot toggle these off.
        // Optional  = IsAdminControlled false: users can disable push for these.
        var expected = new[]
        {
            new NotificationTypeEntity { Id =  1, Name = "General",             Description = "General or test notification",                                  IsAdminControlled = false },
            new NotificationTypeEntity { Id =  2, Name = "PaymentDue",           Description = "A payment obligation is due",                                   IsAdminControlled = true  },
            new NotificationTypeEntity { Id =  3, Name = "PaymentReceived",      Description = "A payment has been recorded",                                   IsAdminControlled = false },
            new NotificationTypeEntity { Id =  4, Name = "CycleCreated",         Description = "A new expense cycle has been created",                          IsAdminControlled = false },
            new NotificationTypeEntity { Id =  5, Name = "SystemRestart",        Description = "System is restarting",                                          IsAdminControlled = true  },
            new NotificationTypeEntity { Id =  6, Name = "GroupInviteReceived",  Description = "A user has been invited to join a group",                       IsAdminControlled = false },
            new NotificationTypeEntity { Id =  7, Name = "CycleStarted",         Description = "An expense cycle has been started and is now active",           IsAdminControlled = false },
            new NotificationTypeEntity { Id =  8, Name = "CyclePaymentMade",     Description = "A member made a payment toward a cycle",                        IsAdminControlled = false },
            new NotificationTypeEntity { Id =  9, Name = "CycleMidReminder",     Description = "Reminder sent at the midpoint of an active cycle",              IsAdminControlled = false },
            new NotificationTypeEntity { Id = 10, Name = "CycleClosingSoon",     Description = "Reminder sent 7 days before a cycle end date",                  IsAdminControlled = true  },
            new NotificationTypeEntity { Id = 11, Name = "CycleClosed",          Description = "An expense cycle has been closed by the admin",                 IsAdminControlled = false },
            new NotificationTypeEntity { Id = 12, Name = "DisputeRaised",        Description = "A member raised a dispute on an expense in a cycle",            IsAdminControlled = true  },
            new NotificationTypeEntity { Id = 13, Name = "DisputeUpdated",       Description = "The admin updated the status of an expense dispute",            IsAdminControlled = true  },
            new NotificationTypeEntity { Id = 14, Name = "ManualReminder",       Description = "Admin manually nudged unsettled members about their balance",   IsAdminControlled = true  },
            new NotificationTypeEntity { Id = 15, Name = "MemberLeftGroup",      Description = "A member left a group; remaining members are notified",         IsAdminControlled = false },
            new NotificationTypeEntity { Id = 16, Name = "MemberJoinedGroup",    Description = "A member accepted a group invite; existing members are notified", IsAdminControlled = false },
            new NotificationTypeEntity { Id = 17, Name = "CycleMemberAdded",     Description = "A user has been added to a Draft expense cycle",                  IsAdminControlled = false },
            new NotificationTypeEntity { Id = 18, Name = "CycleMemberRemoved",   Description = "A user has been removed from a Draft expense cycle",              IsAdminControlled = false },

            // Mukando
            new NotificationTypeEntity { Id = 20, Name = "MukandoRoundStarted",              Description = "A Mukando round has started",                                   IsAdminControlled = false },
            new NotificationTypeEntity { Id = 21, Name = "MukandoContributionReceived",       Description = "A member submitted their contribution",                         IsAdminControlled = false },
            new NotificationTypeEntity { Id = 22, Name = "MukandoContributionConfirmed",      Description = "Admin confirmed a member's contribution",                       IsAdminControlled = false },
            new NotificationTypeEntity { Id = 23, Name = "MukandoAllContributionsCollected",  Description = "All contributions for a round have been collected",              IsAdminControlled = true  },
            new NotificationTypeEntity { Id = 24, Name = "MukandoPayoutConfirmed",            Description = "Payout for a Mukando round has been confirmed",                 IsAdminControlled = false },
            new NotificationTypeEntity { Id = 25, Name = "MukandoRoundCompleted",             Description = "A Mukando round has been completed",                            IsAdminControlled = false },
            new NotificationTypeEntity { Id = 26, Name = "MukandoCycleCompleted",             Description = "All rounds in a Mukando cycle are done",                        IsAdminControlled = false },
            new NotificationTypeEntity { Id = 27, Name = "MukandoSwapRequested",              Description = "A member requested to swap turns",                              IsAdminControlled = false },
            new NotificationTypeEntity { Id = 28, Name = "MukandoSwapAccepted",               Description = "A swap request was accepted",                                   IsAdminControlled = false },
            new NotificationTypeEntity { Id = 29, Name = "MukandoSwapDeclined",               Description = "A swap request was declined",                                   IsAdminControlled = false },
            new NotificationTypeEntity { Id = 30, Name = "MukandoContributionDue",            Description = "Reminder: contribution is due soon or overdue",                 IsAdminControlled = true  },
            new NotificationTypeEntity { Id = 31, Name = "CycleOptOutRequested",              Description = "A member requested to opt out of a cycle",                     IsAdminControlled = false },
            new NotificationTypeEntity { Id = 32, Name = "CycleOptOutResponded",              Description = "Admin responded to an opt-out request",                         IsAdminControlled = false },
        };

        foreach (var e in expected)
        {
            var existing = await context.NotificationTypes.FindAsync(e.Id);
            if (existing == null)
            {
                context.NotificationTypes.Add(e);
            }
            else if (existing.IsAdminControlled != e.IsAdminControlled)
            {
                existing.IsAdminControlled = e.IsAdminControlled;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedCurrenciesAsync(ApplicationDbContext context)
    {
        if (await context.Currencies.AnyAsync()) return;

        var currencies = new[]
        {
            new Currency { Id =  1, Code = "USD", Name = "US Dollar",             Symbol = "$"   },
            new Currency { Id =  2, Code = "ZAR", Name = "South African Rand",    Symbol = "R"   },
            new Currency { Id =  3, Code = "GBP", Name = "British Pound",         Symbol = "£"   },
            new Currency { Id =  4, Code = "EUR", Name = "Euro",                  Symbol = "€"   },
            new Currency { Id =  5, Code = "ZWG", Name = "Zimbabwe Gold",         Symbol = "ZiG" },
            new Currency { Id =  6, Code = "BWP", Name = "Botswana Pula",         Symbol = "P"   },
            new Currency { Id =  7, Code = "KES", Name = "Kenyan Shilling",       Symbol = "KSh" },
            new Currency { Id =  8, Code = "NGN", Name = "Nigerian Naira",        Symbol = "₦"   },
            new Currency { Id =  9, Code = "GHS", Name = "Ghanaian Cedi",         Symbol = "GH₵" },
            new Currency { Id = 10, Code = "TZS", Name = "Tanzanian Shilling",    Symbol = "TSh" },
            new Currency { Id = 11, Code = "UGX", Name = "Ugandan Shilling",      Symbol = "USh" },
            new Currency { Id = 12, Code = "MZN", Name = "Mozambican Metical",    Symbol = "MT"  },
            new Currency { Id = 13, Code = "ZMW", Name = "Zambian Kwacha",        Symbol = "ZK"  },
            new Currency { Id = 14, Code = "MWK", Name = "Malawian Kwacha",       Symbol = "MK"  },
            new Currency { Id = 15, Code = "NAD", Name = "Namibian Dollar",       Symbol = "N$"  },
            new Currency { Id = 16, Code = "AUD", Name = "Australian Dollar",     Symbol = "A$"  },
        };

        context.Currencies.AddRange(currencies);
        await context.SaveChangesAsync();
    }

    private static async Task SeedUserNotificationPreferencesAsync(ApplicationDbContext context)
    {
        // Seed emails must match the users created in SeedUsersAsync.
        var seededEmails = new[] { "carochire@gmail.com", "elroitec@gmail.com" };
        var typeIds      = await context.NotificationTypes.Select(t => t.Id).ToListAsync();

        var userIds = await context.Users
            .Where(u => seededEmails.Contains(u.Email))
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var userId in userIds)
        {
            foreach (var typeId in typeIds)
            {
                var exists = await context.UserNotificationPreferences
                    .AnyAsync(p => p.UserId == userId && p.NotificationTypeId == typeId);

                if (!exists)
                {
                    context.UserNotificationPreferences.Add(new UserNotificationPreference
                    {
                        UserId             = userId,
                        NotificationTypeId = typeId,
                        IsEnabled          = true
                    });
                }
            }
        }

        await context.SaveChangesAsync();
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
                    Email               = "elroitec@gmail.com",
                    FirstName           = "Batanai",
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
        }
    }

    private static async Task SeedAppConfigAsync(ApplicationDbContext context)
    {
        var defaults = new List<AppConfigEntry>
        {
            // System
            new() { Key = AppConfigKeys.AppName,         Value = "Batanai",                     DataType = "string", Category = "System", DisplayName = "Application Name",                Description = "Display name of the application.",                                                  IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.FrontendVersion, Value = "1.0.0",                        DataType = "string", Category = "System", DisplayName = "Frontend Version",                Description = "Current Angular client version.",                                                   IsReadOnly = false, RequiresRestart = true  },
            new() { Key = AppConfigKeys.BackendVersion,  Value = "1.0.0",                        DataType = "string", Category = "System", DisplayName = "Backend Version",                 Description = "Current .NET API version.",                                                         IsReadOnly = false, RequiresRestart = true  },
            new() { Key = AppConfigKeys.DefaultTimeZone, Value = "AUS Eastern Standard Time",    DataType = "string", Category = "System", DisplayName = "Default Time Zone",               Description = "Windows time-zone ID used for date/time display.",                                  IsReadOnly = false, RequiresRestart = true  },
            new() { Key = AppConfigKeys.AppDomain,       Value = "https://batanai.elroitec.com",   DataType = "string", Category = "System", DisplayName = "Application Domain",              Description = "Base URL of the frontend. Used for email links and URL generation.",               IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.DocsUrl,         Value = "https://batanai-docs.pages.dev", DataType = "string", Category = "System", DisplayName = "Technical Documentation URL",     Description = "URL of the hosted technical documentation site.",                                   IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SystemRestartNoticeDelaySeconds, Value = "10",            DataType = "int",    Category = "System", DisplayName = "Restart Notice Delay (seconds)",  Description = "Seconds to wait after sending the restart notification before stopping.",          IsReadOnly = false, RequiresRestart = false },

            // Authentication
            new() { Key = AppConfigKeys.PasswordExpirationDays,            Value = "30",               DataType = "int",    Category = "Authentication", DisplayName = "Password Expiration (days)",               Description = "Days before a password must be changed.",                        IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.JwtExpirationMinutes,              Value = "120",              DataType = "int",    Category = "Authentication", DisplayName = "JWT Token Expiry (minutes)",               Description = "How long a JWT access token remains valid.",                     IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.AdminSignupPin,                    Value = "BATANAI-ADMIN-2026", DataType = "string", Category = "Authentication", DisplayName = "Administrator Signup PIN",                 Description = "PIN required when a new Admin account self-registers.",          IsReadOnly = false, RequiresRestart = false, IsSecret = true },
            new() { Key = AppConfigKeys.PasswordResetTokenValidityMinutes, Value = "15",               DataType = "int",    Category = "Authentication", DisplayName = "Password Reset Token Validity (minutes)",  Description = "How long a password reset token remains valid.",                  IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.PasswordResetRequestLimitPerHour,  Value = "3",                DataType = "int",    Category = "Authentication", DisplayName = "Password Reset Requests Per Hour",         Description = "Max password reset requests per email per hour.",                IsReadOnly = false, RequiresRestart = false },

            // Email / SMTP
            new() { Key = AppConfigKeys.SmtpHost,                            Value = "smtp.gmail.com",                DataType = "string", Category = "Email", DisplayName = "SMTP Host",                               Description = "SMTP server hostname.",                                          IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpPort,                            Value = "587",                           DataType = "int",    Category = "Email", DisplayName = "SMTP Port",                               Description = "SMTP server port.",                                              IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpUsername,                        Value = "elroitec@gmail.com",            DataType = "string", Category = "Email", DisplayName = "SMTP Username",                           Description = "Email address for SMTP authentication.",                         IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpPassword,                        Value = "",                              DataType = "string", Category = "Email", DisplayName = "SMTP Password",                           Description = "Password for SMTP authentication. KEEP CONFIDENTIAL.",          IsReadOnly = false, RequiresRestart = false, IsSecret = true },
            new() { Key = AppConfigKeys.SmtpFromEmail,                       Value = "noreply@elroitec.com",          DataType = "string", Category = "Email", DisplayName = "SMTP From Email",                         Description = "Sender email address.",                                          IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.SmtpFromName,                        Value = "Batanai",                       DataType = "string", Category = "Email", DisplayName = "SMTP From Name",                          Description = "Sender display name for automated emails.",                      IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ForgotPasswordResetUrlPath,          Value = "/reset-password?token={token}", DataType = "string", Category = "Email", DisplayName = "Password Reset URL Path",                 Description = "URL path for password reset. Must include {token}.",             IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailVerificationUrlPath,            Value = "/verify-email?token={token}",   DataType = "string", Category = "Email", DisplayName = "Email Verification URL Path",             Description = "URL path for email verification. Must include {token}.",         IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailVerificationTokenValidityHours, Value = "24",                            DataType = "int",    Category = "Email", DisplayName = "Email Verification Token Validity (hours)", Description = "How long an email verification token remains valid.",            IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailVerificationResendLimitPerHour, Value = "3",                             DataType = "int",    Category = "Email", DisplayName = "Email Verification Resend Limit Per Hour", Description = "Max resend requests per email per hour.",                        IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.EmailBccAddress,                    Value = "",                               DataType = "string", Category = "Email", DisplayName = "Email BCC Address",                        Description = "If set, all outbound emails will be BCC'd to this address.",     IsReadOnly = false, RequiresRestart = false },

            // Ledger
            new() { Key = AppConfigKeys.MembersPerCycle,             Value = "4", DataType = "int", Category = "Ledger", DisplayName = "Members Per Cycle",               Description = "Default number of members expected to contribute per expense cycle.", IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.PaymentReminderIntervalDays, Value = "3", DataType = "int", Category = "Ledger", DisplayName = "Payment Reminder Interval (days)", Description = "Minimum days between automated payment-due reminders for the same user.", IsReadOnly = false, RequiresRestart = false },

            // Reminders
            new() { Key = AppConfigKeys.ReminderMilestoneInitialPct,  Value = "0",  DataType = "int", Category = "Reminders", DisplayName = "Initial Reminder (%)",            Description = "Percentage of cycle duration at which the first payment reminder fires.",             IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ReminderMilestoneMidPct,      Value = "50", DataType = "int", Category = "Reminders", DisplayName = "Midpoint Reminder (%)",           Description = "Percentage of cycle duration at which the midpoint payment reminder fires.",          IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ReminderMilestoneNearDuePct,  Value = "85", DataType = "int", Category = "Reminders", DisplayName = "Near-Due Reminder (%)",           Description = "Percentage of cycle duration at which the near-due payment reminder fires.",          IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ReminderOverdueOffsetDays,    Value = "1",  DataType = "int", Category = "Reminders", DisplayName = "Overdue Offset (days)",           Description = "Days after cycle end date at which the overdue payment reminder fires.",              IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ReminderMinSpacingHours,      Value = "4",  DataType = "int", Category = "Reminders", DisplayName = "Min Spacing (hours)",             Description = "Minimum hours between two reminder milestones (closer ones are dropped).",            IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.ReminderMaxPerCycle,          Value = "4",  DataType = "int", Category = "Reminders", DisplayName = "Max Reminders Per Cycle",         Description = "Maximum number of reminder milestones per user per cycle.",                           IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.KycReminderInitialDelayHours, Value = "1",  DataType = "int", Category = "Reminders", DisplayName = "KYC Initial Delay (hours)",       Description = "Hours after member added to Mukando cycle before first KYC reminder.",                IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.KycReminderFollowUpDays,      Value = "3",  DataType = "int", Category = "Reminders", DisplayName = "KYC Follow-Up (days)",            Description = "Days after member added for the KYC follow-up reminder.",                             IsReadOnly = false, RequiresRestart = false },
            new() { Key = AppConfigKeys.KycReminderEscalationDays,    Value = "7",  DataType = "int", Category = "Reminders", DisplayName = "KYC Escalation (days)",           Description = "Days after member added for the KYC escalation reminder.",                            IsReadOnly = false, RequiresRestart = false },
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
