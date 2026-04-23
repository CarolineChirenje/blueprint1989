namespace Blueprint1989.Api.Models;

/// <summary>
/// Central registry of all push notification types.
/// To add a new type: add a value here and call IPushNotificationSender from the relevant service.
/// </summary>
public enum NotificationType
{
    /// <summary>General or test notification.</summary>
    General = 1,

    /// <summary>The API service is about to restart; all users are notified before shutdown.</summary>
    SystemRestart = 5,

    /// <summary>A feature/bug report status has been updated by an admin.</summary>
    FeatureBugReportResolved = 19,
}

