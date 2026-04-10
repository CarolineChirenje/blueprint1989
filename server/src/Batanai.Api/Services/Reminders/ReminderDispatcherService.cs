using Batanai.Api.Data;
using Batanai.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Batanai.Api.Services.Reminders;

public class ReminderDispatcherService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(2);
    private const int BatchSize = 50;
    private const int MaxRetries = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderDispatcherService> _logger;

    public ReminderDispatcherService(IServiceScopeFactory scopeFactory, ILogger<ReminderDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderDispatcherService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                await DispatchDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReminderDispatcherService.");
            }
        }

        _logger.LogInformation("ReminderDispatcherService stopped.");
    }

    private async Task DispatchDueJobsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();

        var dueJobs = await context.ReminderJobs
            .Where(r => r.Status == ReminderJobStatus.Pending && r.ScheduledForUtc <= DateTime.UtcNow)
            .OrderBy(r => r.ScheduledForUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (dueJobs.Count == 0) return;

        int sent = 0, skipped = 0, failed = 0;

        foreach (var job in dueJobs)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Recheck eligibility
                if (!await IsStillEligibleAsync(context, job, ct))
                {
                    job.Status = ReminderJobStatus.Skipped;
                    job.CancelledAtUtc = DateTime.UtcNow;
                    skipped++;
                    continue;
                }

                // Recompute body with fresh amounts for payment reminders
                var body = job.Body;
                if (job.SubjectType == "Cycle" && job.NotificationType == NotificationType.PaymentDue)
                {
                    body = await GetFreshPaymentBodyAsync(context, job, ct) ?? job.Body;
                }

                await push.SendToUserAsync(
                    job.UserId,
                    job.NotificationType,
                    job.Title,
                    body,
                    job.DeepLinkUrl,
                    job.RelatedEntityId);

                job.Status = ReminderJobStatus.Sent;
                job.SentAtUtc = DateTime.UtcNow;
                sent++;
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;

                if (job.RetryCount >= MaxRetries)
                {
                    job.Status = ReminderJobStatus.Failed;
                    failed++;
                    _logger.LogWarning(ex, "ReminderDispatcher: job {JobId} failed permanently after {Retries} retries.", job.Id, job.RetryCount);
                }
                else
                {
                    _logger.LogWarning(ex, "ReminderDispatcher: job {JobId} failed (attempt {Retry}/{Max}), will retry.", job.Id, job.RetryCount, MaxRetries);
                }
            }
        }

        await context.SaveChangesAsync(ct);

        if (sent > 0 || skipped > 0 || failed > 0)
            _logger.LogInformation("ReminderDispatcher: sent={Sent}, skipped={Skipped}, failed={Failed}.", sent, skipped, failed);
    }

    private static async Task<bool> IsStillEligibleAsync(ApplicationDbContext context, ReminderJob job, CancellationToken ct)
    {
        if (job.SubjectType == "Cycle")
        {
            // Payment reminder: check cycle is still active and user still has unsettled obligations
            var cycle = await context.ExpenseCycles.FindAsync(new object[] { job.SubjectId }, ct);
            if (cycle == null || cycle.Status != CycleStatus.Active) return false;

            var hasUnsettled = await context.MemberObligations
                .AnyAsync(o => context.Expenses
                    .Where(e => e.ExpenseCycleId == job.SubjectId)
                    .Select(e => e.Id)
                    .Contains(o.ExpenseId)
                    && o.UserId == job.UserId
                    && !o.IsSettled, ct);

            return hasUnsettled;
        }

        if (job.SubjectType == "KycCycle")
        {
            // KYC reminder: check user is still unverified and still a member of a Draft cycle
            var user = await context.Users.FindAsync(new object[] { job.UserId }, ct);
            if (user == null) return false;
            if (user.KycStatus == KycStatus.Verified || user.KycStatus == KycStatus.AdminBypassed) return false;

            var cycle = await context.ExpenseCycles.FindAsync(new object[] { job.SubjectId }, ct);
            if (cycle == null || cycle.Status != CycleStatus.Draft) return false;

            var isMember = await context.CycleMembers
                .AnyAsync(m => m.ExpenseCycleId == job.SubjectId && m.UserId == job.UserId, ct);

            return isMember;
        }

        return true; // Unknown subject type — let it through
    }

    private static async Task<string?> GetFreshPaymentBodyAsync(ApplicationDbContext context, ReminderJob job, CancellationToken ct)
    {
        var cycle = await context.ExpenseCycles.FindAsync(new object[] { job.SubjectId }, ct);
        if (cycle == null) return null;

        var currency = await context.Currencies.FindAsync(new object[] { cycle.CurrencyId }, ct);
        var symbol = currency?.Symbol ?? "$";

        var expenseIds = await context.Expenses
            .Where(e => e.ExpenseCycleId == job.SubjectId)
            .Select(e => e.Id)
            .ToListAsync(ct);

        var totalOwed = await context.MemberObligations
            .Where(o => expenseIds.Contains(o.ExpenseId) && o.UserId == job.UserId && !o.IsSettled)
            .SumAsync(o => o.AmountOwed, ct);

        if (totalOwed <= 0) return null;

        return job.StageKey switch
        {
            "Initial" => $"You have {symbol}{totalOwed:F2} in outstanding payments for {cycle.Name}. Due by {cycle.EndDate:MMM d, yyyy}.",
            "Midpoint" => $"Halfway through {cycle.Name}. You still owe {symbol}{totalOwed:F2}. Due by {cycle.EndDate:MMM d, yyyy}.",
            "NearDue" => $"{cycle.Name} is ending soon. You still owe {symbol}{totalOwed:F2}. Due by {cycle.EndDate:MMM d, yyyy}.",
            "Overdue" => $"{cycle.Name} has ended and you still owe {symbol}{totalOwed:F2}. Please settle up.",
            _ => $"You owe {symbol}{totalOwed:F2} for {cycle.Name}."
        };
    }
}
