using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.Models;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[Route("api/system")]
[ApiController]
[Authorize]
public class SystemController : ControllerBase
{
    private readonly ILogger<SystemController> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _scopeFactory;

    public SystemController(
        ILogger<SystemController> logger,
        IHostApplicationLifetime lifetime,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _lifetime = lifetime;
        _scopeFactory = scopeFactory;
    }

    // POST /api/system/restart
    [HttpPost("restart")]
    public IActionResult Restart()
    {
        var roleStr = User.FindFirst("role")?.Value;
        if (roleStr != "SuperAdmin" && roleStr != "Administrator")
            return Forbid();

        var userId = User.FindFirst("id")?.Value;
        _logger.LogWarning("System restart requested by user {UserId} (role: {Role})", userId, roleStr);

        // Notify all users with active push subscriptions, then trigger graceful shutdown.
        // The host process manager (systemd / Docker) is responsible for restarting.
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context    = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var pushSender = scope.ServiceProvider.GetRequiredService<IPushNotificationSender>();
                var appConfig  = scope.ServiceProvider.GetRequiredService<AppConfigService>();

                var delaySeconds = await appConfig.GetIntAsync(AppConfigKeys.SystemRestartNoticeDelaySeconds, 10);

                var userIds = await context.PushSubscriptions
                    .Select(s => s.UserId)
                    .Distinct()
                    .ToListAsync();

                if (userIds.Count > 0)
                {
                    await pushSender.SendToUsersAsync(
                        userIds,
                        NotificationType.SystemRestart,
                        "System Restart",
                        $"Batanai is restarting in {delaySeconds} second{(delaySeconds == 1 ? "" : "s")}. It will be back shortly.",
                        deepLinkUrl: "/dashboard");
                }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send system restart push notifications.");
                await Task.Delay(TimeSpan.FromSeconds(3)); // fallback delay
            }

            _lifetime.StopApplication();
        });

        return Accepted(new { message = "Service restart initiated." });
    }
}
