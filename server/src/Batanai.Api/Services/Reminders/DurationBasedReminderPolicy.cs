using Batanai.Api.Models;

namespace Batanai.Api.Services.Reminders;

public class DurationBasedReminderPolicy : IReminderPolicy
{
    private readonly AppConfigService _config;

    public DurationBasedReminderPolicy(AppConfigService config)
    {
        _config = config;
    }

    public async Task<List<ReminderMilestone>> CalculateMilestonesAsync(DateTime startUtc, DateTime endUtc)
    {
        var initialPct   = await _config.GetIntAsync(AppConfigKeys.ReminderMilestoneInitialPct, 0);
        var midPct       = await _config.GetIntAsync(AppConfigKeys.ReminderMilestoneMidPct, 50);
        var nearDuePct   = await _config.GetIntAsync(AppConfigKeys.ReminderMilestoneNearDuePct, 85);
        var overdueDays  = await _config.GetIntAsync(AppConfigKeys.ReminderOverdueOffsetDays, 1);
        var minSpacingH  = await _config.GetIntAsync(AppConfigKeys.ReminderMinSpacingHours, 4);
        var maxPerCycle  = await _config.GetIntAsync(AppConfigKeys.ReminderMaxPerCycle, 4);

        var duration = endUtc - startUtc;
        var totalHours = duration.TotalHours;

        var candidates = new List<ReminderMilestone>();

        // Always include Initial
        candidates.Add(new ReminderMilestone(
            "Initial",
            startUtc.AddHours(totalHours * initialPct / 100.0),
            NotificationType.PaymentDue,
            "Payment Reminder",
            "You have {amount} in outstanding payments for {cycleName}. Due by {dueDate}."));

        // Midpoint: only for cycles >= 7 days
        if (totalHours >= 7 * 24)
        {
            candidates.Add(new ReminderMilestone(
                "Midpoint",
                startUtc.AddHours(totalHours * midPct / 100.0),
                NotificationType.PaymentDue,
                "Midpoint Reminder",
                "Halfway through {cycleName}. You still owe {amount}. Due by {dueDate}."));
        }

        // NearDue: for cycles >= 24h
        if (totalHours >= 24)
        {
            candidates.Add(new ReminderMilestone(
                "NearDue",
                startUtc.AddHours(totalHours * nearDuePct / 100.0),
                NotificationType.PaymentDue,
                "Payment Due Soon",
                "{cycleName} is ending soon. You still owe {amount}. Due by {dueDate}."));
        }

        // Overdue: always include
        candidates.Add(new ReminderMilestone(
            "Overdue",
            endUtc.AddDays(overdueDays),
            NotificationType.PaymentDue,
            "Payment Overdue",
            "{cycleName} has ended and you still owe {amount}. Please settle up."));

        // Apply minimum spacing — drop the earlier candidate (except Initial and Overdue)
        var minSpacing = TimeSpan.FromHours(minSpacingH);
        var filtered = new List<ReminderMilestone> { candidates[0] }; // keep Initial

        for (int i = 1; i < candidates.Count; i++)
        {
            var prev = filtered[^1];
            var curr = candidates[i];

            if (curr.ScheduledForUtc - prev.ScheduledForUtc < minSpacing)
            {
                // Keep Overdue and Initial, drop middle ones
                if (curr.StageKey == "Overdue")
                    filtered.Add(curr);
                // else: drop this candidate
            }
            else
            {
                filtered.Add(curr);
            }
        }

        // Cap at max
        if (filtered.Count > maxPerCycle)
        {
            // Always keep Initial (first) and Overdue (last if present)
            var hasOverdue = filtered[^1].StageKey == "Overdue";
            if (hasOverdue)
            {
                var middle = filtered.Skip(1).Take(filtered.Count - 2).ToList();
                var keep = middle.Take(maxPerCycle - 2).ToList();
                filtered = new List<ReminderMilestone> { filtered[0] };
                filtered.AddRange(keep);
                filtered.Add(filtered[^1].StageKey == "Overdue" ? filtered[^1] : candidates[^1]);
            }
            else
            {
                filtered = filtered.Take(maxPerCycle).ToList();
            }
        }

        return filtered.OrderBy(m => m.ScheduledForUtc).ToList();
    }
}
