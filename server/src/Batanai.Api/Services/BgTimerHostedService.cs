using Batanai.Api.Data;
using Batanai.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Batanai.Api.Services;

/// <summary>
/// Background service that wakes periodically to dispatch scheduled push notifications.
/// Payment-reminder logic will be added in Phase 6.
/// </summary>
public class BgTimerHostedService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BgTimerHostedService> _logger;

    public BgTimerHostedService(IServiceScopeFactory scopeFactory, ILogger<BgTimerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BgTimerHostedService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in BgTimerHostedService.");
            }
        }

        _logger.LogInformation("BgTimerHostedService stopped.");
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context     = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var push        = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

        // Run each job independently so a failure in one doesn't skip the others.
        await RunJobAsync("PaymentReminders", () => SendPaymentRemindersAsync(context, push, ct));
        await RunJobAsync("KycReminders",     () => SendKycRemindersAsync(context, push, ct));
    }

    /// <summary>Runs a named job, logging start/finish and isolating exceptions from sibling jobs.</summary>
    private async Task RunJobAsync(string jobName, Func<Task> job)
    {
        try
        {
            _logger.LogDebug("BgTimer: starting job {Job}.", jobName);
            await job();
            _logger.LogDebug("BgTimer: finished job {Job}.", jobName);
        }
        catch (OperationCanceledException)
        {
            throw; // propagate so the outer loop can exit cleanly
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BgTimer: job {Job} failed.", jobName);
        }
    }

    private async Task SendPaymentRemindersAsync(ApplicationDbContext context, IPushNotificationSender push, CancellationToken ct)
    {
        var activeCycleIds = await context.ExpenseCycles
            .Where(c => c.Status == CycleStatus.Active)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (activeCycleIds.Count == 0) return;

        var expenseIds = await context.Expenses
            .Where(e => activeCycleIds.Contains(e.ExpenseCycleId))
            .Select(e => e.Id)
            .ToListAsync(ct);

        var debtorIds = await context.MemberObligations
            .Where(o => expenseIds.Contains(o.ExpenseId) && !o.IsSettled)
            .Select(o => o.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (debtorIds.Count == 0) return;

        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Batch deduplication: fetch all already-notified user IDs in one query.
        var alreadyNotifiedIds = await context.Notifications
            .Where(n => debtorIds.Contains(n.UserId)
                     && n.Type == NotificationType.PaymentDue
                     && n.CreatedAt >= cutoff)
            .Select(n => n.UserId)
            .Distinct()
            .ToListAsync(ct);

        var toNotify = debtorIds.Except(alreadyNotifiedIds).ToList();
        int sent = 0;

        foreach (var userId in toNotify)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var total = await context.MemberObligations
                    .Where(o => expenseIds.Contains(o.ExpenseId) && o.UserId == userId && !o.IsSettled)
                    .SumAsync(o => o.AmountOwed, ct);

                await push.SendToUserAsync(
                    userId,
                    NotificationType.PaymentDue,
                    "Payment Reminder",
                    $"You have ${total:F2} in outstanding payments. Head to your cycles to settle up.",
                    "/cycles");

                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BgTimer: PaymentReminder failed for user {UserId}.", userId);
            }
        }

        if (sent > 0)
            _logger.LogInformation("BgTimer: sent {Count} PaymentDue notification(s).", sent);
    }

    /// <summary>
    /// Notifies users who are members of a Mukando Draft cycle but have not yet completed KYC.
    /// Skips users who already received a KycReminder in the last 24 hours.
    /// </summary>
    private async Task SendKycRemindersAsync(ApplicationDbContext context, IPushNotificationSender push, CancellationToken ct)
    {
        var mukandoDraftCycleIds = await context.ExpenseCycles
            .Where(c => c.CycleType == CycleType.Mukando && c.Status == CycleStatus.Draft)
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (mukandoDraftCycleIds.Count == 0) return;

        var pendingMemberUserIds = await context.CycleMembers
            .Where(m => mukandoDraftCycleIds.Contains(m.ExpenseCycleId))
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(ct);

        if (pendingMemberUserIds.Count == 0) return;

        // Filter to only those without KYC sign-off
        var unverifiedUserIds = await context.Users
            .Where(u => pendingMemberUserIds.Contains(u.Id)
                     && u.KycStatus != KycStatus.Verified
                     && u.KycStatus != KycStatus.AdminBypassed)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (unverifiedUserIds.Count == 0) return;

        var kycCutoff = DateTime.UtcNow.AddHours(-24);

        // Batch deduplication: one query instead of one per user.
        var alreadyNotifiedIds = await context.Notifications
            .Where(n => unverifiedUserIds.Contains(n.UserId)
                     && n.Type == NotificationType.KycReminder
                     && n.CreatedAt >= kycCutoff)
            .Select(n => n.UserId)
            .Distinct()
            .ToListAsync(ct);

        var toNotify = unverifiedUserIds.Except(alreadyNotifiedIds).ToList();
        int sent = 0;

        foreach (var userId in toNotify)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await push.SendToUserAsync(
                    userId,
                    NotificationType.KycReminder,
                    "Verify Your Identity",
                    "You are part of a Mukando cycle that requires identity verification. Complete your KYC in your profile to avoid being blocked when the cycle starts.",
                    "/profile");

                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BgTimer: KycReminder failed for user {UserId}.", userId);
            }
        }

        if (sent > 0)
            _logger.LogInformation("BgTimer: sent {Count} KycReminder notification(s).", sent);
    }
}