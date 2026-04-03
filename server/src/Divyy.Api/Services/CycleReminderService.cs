using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
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
    }
}
