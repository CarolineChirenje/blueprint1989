using Divvy.Api.Models;

namespace Divvy.Api.Services;

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
    /// Sends a push notification to multiple users in parallel.
    /// </summary>
    Task SendToUsersAsync(
        IEnumerable<int> userIds,
        NotificationType type,
        string title,
        string body,
        string? deepLinkUrl = null,
        int? relatedEntityId = null);
}
