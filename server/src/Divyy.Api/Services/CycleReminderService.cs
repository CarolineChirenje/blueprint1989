using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

/// <summary>
/// Background service that runs every 6 hours and sends reminder push notifications
/// for active cycles at two trigger points:
///   1. Mid-point of the cycle duration
///   2. 7 days before the EndDate
/// </summary>
public class CycleReminderService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly ILogger<CycleReminderService> _logger;

    public CycleReminderService(IServiceScopeFactory scopeFactory, ILogger<CycleReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[CycleReminderService] Started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCheckAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CycleReminderService] Error during reminder check.");
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task RunCheckAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var push    = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

        var now    = DateTime.UtcNow;
        var cycles = await context.ExpenseCycles
            .Where(c => c.Status == CycleStatus.Active)
            .ToListAsync();

        foreach (var cycle in cycles)
        {
            var memberIds = await context.CycleMembers
                .Where(m => m.ExpenseCycleId == cycle.Id)
                .Select(m => m.UserId)
                .ToListAsync();

            if (memberIds.Count == 0) continue;

            var deepLink = $"/cycles/{cycle.Id}";

            // Mid-point reminder
            if (!cycle.MidReminderSent)
            {
                var midPoint = cycle.StartDate + TimeSpan.FromTicks((cycle.EndDate - cycle.StartDate).Ticks / 2);
                if (now >= midPoint)
                {
                    await push.SendToUsersAsync(
                        memberIds,
                        NotificationType.CycleMidReminder,
                        $"Halfway reminder: {cycle.Name}",
                        $"The cycle \"{cycle.Name}\" is at its midpoint. Make sure payments are on track!",
                        deepLink,
                        cycle.Id);

                    cycle.MidReminderSent = true;
                    _logger.LogInformation("[CycleReminderService] Sent mid-reminder for cycle {Id}.", cycle.Id);
                }
            }

            // Closing-soon reminder (7 days before end)
            if (!cycle.ClosingSoonSent)
            {
                var closingSoonThreshold = cycle.EndDate.AddDays(-7);
                if (now >= closingSoonThreshold)
                {
                    await push.SendToUsersAsync(
                        memberIds,
                        NotificationType.CycleClosingSoon,
                        $"Cycle closing soon: {cycle.Name}",
                        $"The cycle \"{cycle.Name}\" closes on {cycle.EndDate:MMM d, yyyy}. Ensure all payments are submitted.",
                        deepLink,
                        cycle.Id);

                    cycle.ClosingSoonSent = true;
                    _logger.LogInformation("[CycleReminderService] Sent closing-soon reminder for cycle {Id}.", cycle.Id);
                }
            }
        }

        if (cycles.Any(c => c.MidReminderSent || c.ClosingSoonSent))
            await context.SaveChangesAsync();

        // ── Mukando tiered contribution reminders ───────────────────
        await RunMukandoRemindersAsync(context, push);
    }

    private async Task RunMukandoRemindersAsync(ApplicationDbContext context, IPushNotificationSender push)
    {
        var now = DateTime.UtcNow;

        var activeRounds = await context.MukandoRounds
            .Where(r => r.Status == RoundStatus.Active)
            .ToListAsync();

        foreach (var round in activeRounds)
        {
            var cycle = await context.ExpenseCycles.FindAsync(round.ExpenseCycleId);
            if (cycle == null) continue;

            var currency = await context.Currencies.FindAsync(cycle.CurrencyId);
            var sym = currency?.Symbol ?? "$";
            var deepLink = $"/cycles/{cycle.Id}";

            var pendingContributions = await context.MukandoContributions
                .Where(c => c.MukandoRoundId == round.Id && c.Status == ContributionStatus.Pending)
                .ToListAsync();

            var daysUntilDue = (round.DueDate - now).TotalDays;
            var daysOverdue = (now - round.DueDate).TotalDays;

            foreach (var contrib in pendingContributions)
            {
                // Tier 1: 2 days before due
                if (!contrib.Reminder1Sent && daysUntilDue <= 2 && daysUntilDue > 0)
                {
                    await push.SendToUserAsync(
                        contrib.UserId,
                        NotificationType.MukandoContributionDue,
                        $"Contribution due soon: Round {round.RoundNumber}",
                        $"Your {sym}{contrib.Amount:F2} contribution for Round {round.RoundNumber} of \"{cycle.Name}\" is due in {Math.Ceiling(daysUntilDue)} day(s).",
                        deepLink, cycle.Id);

                    contrib.Reminder1Sent = true;
                    _logger.LogInformation("[CycleReminderService] Sent Mukando reminder-1 for contribution {Id}.", contrib.Id);
                }

                // Tier 2: 1 day overdue — remind member
                if (!contrib.Reminder2Sent && daysOverdue >= 1 && daysOverdue < 3)
                {
                    await push.SendToUserAsync(
                        contrib.UserId,
                        NotificationType.MukandoContributionDue,
                        $"Contribution overdue: Round {round.RoundNumber}",
                        $"Your {sym}{contrib.Amount:F2} contribution for \"{cycle.Name}\" Round {round.RoundNumber} is overdue by 1 day.",
                        deepLink, cycle.Id);

                    contrib.Reminder2Sent = true;
                    _logger.LogInformation("[CycleReminderService] Sent Mukando reminder-2 for contribution {Id}.", contrib.Id);
                }

                // Tier 3: 3+ days overdue — escalate to admins
                if (!contrib.EscalationSent && daysOverdue >= 3)
                {
                    var member = await context.Users.FindAsync(contrib.UserId);
                    var memberName = member != null ? $"{member.FirstName} {member.LastName}" : "A member";

                    var groupId = cycle.GroupId;
                    var adminIds = await context.GroupMembers
                        .Where(gm => gm.GroupId == groupId && gm.GroupRole == GroupRole.GroupAdmin && gm.Status == GroupInviteStatus.Accepted)
                        .Select(gm => gm.UserId)
                        .ToListAsync();

                    if (adminIds.Count > 0)
                    {
                        await push.SendToUsersAsync(
                            adminIds,
                            NotificationType.MukandoContributionDue,
                            $"Overdue alert: {memberName} — Round {round.RoundNumber}",
                            $"{memberName}'s {sym}{contrib.Amount:F2} contribution for \"{cycle.Name}\" Round {round.RoundNumber} is {Math.Floor(daysOverdue)} day(s) overdue.",
                            deepLink, cycle.Id);
                    }

                    contrib.EscalationSent = true;
                    _logger.LogInformation("[CycleReminderService] Sent Mukando escalation for contribution {Id}.", contrib.Id);
                }
            }
        }

        await context.SaveChangesAsync();
    }
}
