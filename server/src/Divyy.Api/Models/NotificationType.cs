namespace Divvy.Api.Models;

/// <summary>
/// Central registry of all push notification types.
/// To add a new type: add a value here and call IPushNotificationSender from the relevant service.
/// </summary>
public enum NotificationType
{
    /// <summary>General or test notification.</summary>
    General = 1,

    /// <summary>An outstanding payment obligation is due in a cycle.</summary>
    PaymentDue = 2,

    /// <summary>A payment was submitted or confirmed by another member.</summary>
    PaymentReceived = 3,

    /// <summary>A new expense cycle has been created that includes this member.</summary>
    CycleCreated = 4,

    /// <summary>The API service is about to restart; all users are notified before shutdown.</summary>
    SystemRestart = 5,
}

