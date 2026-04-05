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

        // Find members with unsettled obligations in active cycles,
        // who have not received a PaymentDue notification in the last 24 hours.
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

        var cutoff = DateTime.UtcNow.AddHours(-24);

        foreach (var userId in debtorIds)
        {
            if (ct.IsCancellationRequested) break;

            // Skip if a PaymentDue notification was sent in the last 24 hours
            var alreadyNotified = await context.Notifications
                .AnyAsync(n => n.UserId == userId
                            && n.Type == NotificationType.PaymentDue
                            && n.CreatedAt >= cutoff, ct);

            if (alreadyNotified) continue;

            var total = await context.MemberObligations
                .Where(o => expenseIds.Contains(o.ExpenseId) && o.UserId == userId && !o.IsSettled)
                .SumAsync(o => o.AmountOwed, ct);

            await push.SendToUserAsync(
                userId,
                NotificationType.PaymentDue,
                "Payment Reminder",
                $"You have ${total:F2} in outstanding payments. Head to your cycles to settle up.",
                "/cycles");
        }
    }
}