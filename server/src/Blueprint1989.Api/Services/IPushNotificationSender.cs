using Blueprint1989.Api.Models;

namespace Blueprint1989.Api.Services;

/// <summary>
/// Central push notification interface.
/// To add a new notification trigger: call SendToUserAsync or SendToUsersAsync
/// from the relevant service, passing the appropriate <see cref="NotificationType"/>.
/// </summary>
public interface IPushNotificationSender
{
    /// <summary>
    /// Sends a push notification to a single user (all their subscribed devices).
    /// Also creates a persistent in-app <see cref="Notification"/> row.
    /// </summary>
    Task SendToUserAsync(
        int userId,
        NotificationType type,
        string title,
        string body,
        string? deepLinkUrl = null,
        int? relatedEntityId = null);

    /// <summary>
    /// Sends a push notification to multiple users sequentially.
    /// </summary>
    /// <param name="excludeUserIds">
    /// Optional set of user IDs to skip (e.g. the actor who triggered the event).
    /// Defence-in-depth: callers should also filter their own lists, but this
    /// guarantees the excluded users never receive the notification even if the
    /// caller's query has tracking or race-condition issues.
    /// </param>
    Task SendToUsersAsync(
        IEnumerable<int> userIds,
        NotificationType type,
        string title,
        string body,
        string? deepLinkUrl = null,
        int? relatedEntityId = null,
        IEnumerable<int>? excludeUserIds = null);
}
