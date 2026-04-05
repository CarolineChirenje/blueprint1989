using System.Text.Json;
using Batanai.Api.Data;
using Batanai.Api.Models;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Batanai.Api.Services;

/// <summary>
/// VAPID-signed server-sent push notification delivery.
/// Uses Lib.Net.Http.WebPush under the hood.
/// Every push delivery also persists an in-app Notification row via <see cref="NotificationService"/>.
/// </summary>
public class PushNotificationSender : IPushNotificationSender
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly ILogger<PushNotificationSender> _logger;
    private readonly PushServiceClient _pushClient;

    public PushNotificationSender(
        ApplicationDbContext context,
        NotificationService notificationService,
        IOptions<VapidSettings> vapidOptions,
        HttpClient httpClient,
        ILogger<PushNotificationSender> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;

        var vapid = vapidOptions.Value;

        _pushClient = new PushServiceClient(httpClient);

        var subject = vapid.Subject;
        if (string.IsNullOrWhiteSpace(subject) ||
            (!subject.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
             !subject.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Vapid:Subject is missing or invalid ('{Subject}'). Push notifications will be disabled. Set it to a 'mailto:' or 'https:' URI.", subject);
            _pushClient = null!;
            return;
        }

        _pushClient.DefaultAuthentication = new VapidAuthentication(vapid.PublicKey, vapid.PrivateKey)
        {
            Subject = subject
        };
    }

    /// <inheritdoc/>
    public async Task SendToUserAsync(
        int userId,
        NotificationType type,
        string title,
        string body,
        string? deepLinkUrl = null,
        int? relatedEntityId = null)
    {
        // 1. Create in-app notification row
        await _notificationService.CreateAsync(userId, $"{title}: {body}", type, deepLinkUrl, relatedEntityId, sentViaPush: true);

        if (_pushClient is null) return; // VAPID not configured — in-app notification still created above

        // 2. Check whether this user has opted out of push for this notification type.
        //    In-app notification is always created above regardless of preference.
        var typeId = (int)type;
        var preference = await _context.UserNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationTypeId == typeId);

        if (preference != null && !preference.IsEnabled)
            return; // User opted out of push for this type; in-app notification still created above

        // 3. Load all push subscriptions for this user
        var subscriptions = await _context.PushSubscriptions
            .Where(s => s.UserId == userId)
            .ToListAsync();

        if (subscriptions.Count == 0)
        {
            _logger.LogDebug("No push subscriptions found for user {UserId}; in-app notification created but no push sent.", userId);
            return;
        }

        // 4. Build message payload
        var payload = BuildPayload(type, title, body, deepLinkUrl);
        var expiredEndpoints = new List<int>();

        // 5. Dispatch in parallel, collect expired subscriptions to prune
        await Parallel.ForEachAsync(subscriptions, async (sub, _) =>
        {
            try
            {
                var libSub = ToLibSubscription(sub);
                var message = new PushMessage(payload)
                {
                    TimeToLive = 86400 // 24 hours TTL
                };
                await _pushClient.RequestPushMessageDeliveryAsync(libSub, message);
            }
            catch (PushServiceClientException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
            {
                // 410 = subscription expired; queue for removal
                lock (expiredEndpoints)
                    expiredEndpoints.Add(sub.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send push to subscription {Id} for user {UserId}", sub.Id, userId);
            }
        });

        // 6. Prune expired subscriptions
        if (expiredEndpoints.Count > 0)
        {
            await _context.PushSubscriptions
                .Where(s => expiredEndpoints.Contains(s.Id))
                .ExecuteDeleteAsync();

            _logger.LogInformation("Pruned {Count} expired push subscriptions for user {UserId}",
                expiredEndpoints.Count, userId);
        }
    }

    /// <inheritdoc/>
    public async Task SendToUsersAsync(
        IEnumerable<int> userIds,
        NotificationType type,
        string title,
        string body,
        string? deepLinkUrl = null,
        int? relatedEntityId = null)
    {
        // Process sequentially — all user deliveries share the same scoped DbContext,
        // so concurrent Task.WhenAll would trigger a "second operation started" exception.
        foreach (var uid in userIds)
        {
            await SendToUserAsync(uid, type, title, body, deepLinkUrl, relatedEntityId);
        }
    }

    // --- helpers ------------------------------------------------------------

    private static string BuildPayload(NotificationType type, string title, string body, string? url)
    {
        var payload = new
        {
            title,
            body,
            icon = "/assets/icons/icon-192x192.png",
            badge = "/assets/icons/icon-192x192.png",
            url = url ?? "/",
            type = (int)type,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        return JsonSerializer.Serialize(payload);
    }

    private static Lib.Net.Http.WebPush.PushSubscription ToLibSubscription(Models.PushSubscription sub) =>
        new()
        {
            Endpoint = sub.Endpoint,
            Keys = new Dictionary<string, string>
            {
                ["p256dh"] = sub.P256dh,
                ["auth"]   = sub.Auth
            }
        };
}
