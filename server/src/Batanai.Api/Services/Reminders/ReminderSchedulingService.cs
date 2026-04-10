using Batanai.Api.Data;
using Batanai.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Batanai.Api.Services.Reminders;

public class ReminderSchedulingService
{
    private readonly ApplicationDbContext _context;
    private readonly IReminderPolicy _policy;
    private readonly AppConfigService _config;

    public ReminderSchedulingService(ApplicationDbContext context, IReminderPolicy policy, AppConfigService config)
    {
        _context = context;
        _policy  = policy;
        _config  = config;
    }

    /// <summary>
    /// Creates payment reminder jobs for all users with unsettled obligations in the given cycle.
    /// Called from ExpenseCycleService.StartAsync (the sole trigger).
    /// </summary>
    public async Task ScheduleForCycleAsync(int cycleId)
    {
        var cycle = await _context.ExpenseCycles.FindAsync(cycleId);
        if (cycle == null) return;

        var currency = await _context.Currencies.FindAsync(cycle.CurrencyId);
        var symbol = currency?.Symbol ?? "$";

        var milestones = await _policy.CalculateMilestonesAsync(cycle.StartDate, cycle.EndDate);

        // Get all users with unsettled obligations in this cycle
        var userTotals = await _context.MemberObligations
            .Where(o => _context.Expenses
                .Where(e => e.ExpenseCycleId == cycleId)
                .Select(e => e.Id)
                .Contains(o.ExpenseId) && !o.IsSettled)
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, TotalOwed = g.Sum(o => o.AmountOwed) })
            .ToListAsync();

        if (userTotals.Count == 0 || milestones.Count == 0) return;

        var existingKeys = await _context.ReminderJobs
            .Where(r => r.SubjectType == "Cycle" && r.SubjectId == cycleId)
            .Select(r => r.IdempotencyKey)
            .ToHashSetAsync();

        var jobs = new List<ReminderJob>();

        foreach (var ut in userTotals)
        {
            foreach (var m in milestones)
            {
                var idemKey = $"PaymentDue:{ut.UserId}:{cycleId}:{m.StageKey}";
                if (existingKeys.Contains(idemKey)) continue;

                var body = m.BodyTemplate
                    .Replace("{amount}", $"{symbol}{ut.TotalOwed:F2}")
                    .Replace("{cycleName}", cycle.Name)
                    .Replace("{dueDate}", cycle.EndDate.ToString("MMM d, yyyy"));

                jobs.Add(new ReminderJob
                {
                    UserId           = ut.UserId,
                    NotificationType = m.NotificationType,
                    SubjectType      = "Cycle",
                    SubjectId        = cycleId,
                    StageKey         = m.StageKey,
                    IdempotencyKey   = idemKey,
                    ScheduledForUtc  = m.ScheduledForUtc,
                    Status           = ReminderJobStatus.Pending,
                    Title            = m.TitleTemplate,
                    Body             = body,
                    DeepLinkUrl      = $"/cycles/{cycleId}",
                    RelatedEntityId  = cycleId,
                    CreatedAtUtc     = DateTime.UtcNow
                });
            }
        }

        if (jobs.Count > 0)
        {
            _context.ReminderJobs.AddRange(jobs);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>Cancels all pending reminder jobs for a cycle (called on cycle close).</summary>
    public async Task CancelForCycleAsync(int cycleId)
    {
        await _context.ReminderJobs
            .Where(r => r.SubjectType == "Cycle" && r.SubjectId == cycleId && r.Status == ReminderJobStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReminderJobStatus.Cancelled)
                .SetProperty(r => r.CancelledAtUtc, DateTime.UtcNow));
    }

    /// <summary>Cancels all pending payment reminders for a specific user in a cycle (called when user fully settles).</summary>
    public async Task CancelForUserInCycleAsync(int userId, int cycleId)
    {
        await _context.ReminderJobs
            .Where(r => r.UserId == userId
                     && r.SubjectId == cycleId
                     && (r.SubjectType == "Cycle" || r.SubjectType == "KycCycle")
                     && r.Status == ReminderJobStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReminderJobStatus.Cancelled)
                .SetProperty(r => r.CancelledAtUtc, DateTime.UtcNow));
    }

    /// <summary>Cancels and re-creates reminders for a cycle (called when cycle dates/obligations change).</summary>
    public async Task RescheduleForCycleAsync(int cycleId)
    {
        await CancelForCycleAsync(cycleId);
        await ScheduleForCycleAsync(cycleId);
    }

    /// <summary>
    /// Schedules KYC reminder jobs for a user added to a Mukando Draft cycle who is not yet verified.
    /// </summary>
    public async Task ScheduleKycRemindersAsync(int userId, int cycleId)
    {
        var initialDelayH = await _config.GetIntAsync(AppConfigKeys.KycReminderInitialDelayHours, 1);
        var followUpDays  = await _config.GetIntAsync(AppConfigKeys.KycReminderFollowUpDays, 3);
        var escalationDays = await _config.GetIntAsync(AppConfigKeys.KycReminderEscalationDays, 7);

        var now = DateTime.UtcNow;
        var stages = new[]
        {
            ("KycInitial",    now.AddHours(initialDelayH),  "Verify Your Identity",     "You've been added to a Mukando cycle that requires identity verification. Complete your KYC in your profile."),
            ("KycFollowUp",   now.AddDays(followUpDays),    "KYC Reminder",             "You still need to complete identity verification to participate in your Mukando cycle. Please verify in your profile."),
            ("KycEscalation", now.AddDays(escalationDays),  "Urgent: KYC Required",     "Your Mukando cycle cannot start until you verify your identity. Complete KYC now to avoid being removed.")
        };

        var existingKeys = await _context.ReminderJobs
            .Where(r => r.SubjectType == "KycCycle" && r.SubjectId == cycleId && r.UserId == userId)
            .Select(r => r.IdempotencyKey)
            .ToHashSetAsync();

        var jobs = new List<ReminderJob>();

        foreach (var (stageKey, scheduledFor, title, body) in stages)
        {
            var idemKey = $"KycReminder:{userId}:{cycleId}:{stageKey}";
            if (existingKeys.Contains(idemKey)) continue;

            jobs.Add(new ReminderJob
            {
                UserId           = userId,
                NotificationType = NotificationType.KycReminder,
                SubjectType      = "KycCycle",
                SubjectId        = cycleId,
                StageKey         = stageKey,
                IdempotencyKey   = idemKey,
                ScheduledForUtc  = scheduledFor,
                Status           = ReminderJobStatus.Pending,
                Title            = title,
                Body             = body,
                DeepLinkUrl      = "/profile",
                RelatedEntityId  = cycleId,
                CreatedAtUtc     = DateTime.UtcNow
            });
        }

        if (jobs.Count > 0)
        {
            _context.ReminderJobs.AddRange(jobs);
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>Cancels all pending KYC reminders for a user (called when KYC is verified or bypassed).</summary>
    public async Task CancelKycRemindersForUserAsync(int userId)
    {
        await _context.ReminderJobs
            .Where(r => r.UserId == userId
                     && r.NotificationType == NotificationType.KycReminder
                     && r.Status == ReminderJobStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReminderJobStatus.Cancelled)
                .SetProperty(r => r.CancelledAtUtc, DateTime.UtcNow));
    }

    /// <summary>Cancels all pending KYC reminders for a cycle (called when cycle starts or is deleted).</summary>
    public async Task CancelKycRemindersForCycleAsync(int cycleId)
    {
        await _context.ReminderJobs
            .Where(r => r.SubjectType == "KycCycle" && r.SubjectId == cycleId && r.Status == ReminderJobStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReminderJobStatus.Cancelled)
                .SetProperty(r => r.CancelledAtUtc, DateTime.UtcNow));
    }
}
