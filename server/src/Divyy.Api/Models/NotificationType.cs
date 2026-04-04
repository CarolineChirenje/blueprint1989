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

    /// <summary>A user has been invited to join a group.</summary>
    GroupInviteReceived = 6,

    /// <summary>An expense cycle has been started by the admin and is now active.</summary>
    CycleStarted = 7,

    /// <summary>A member made a payment toward a cycle.</summary>
    CyclePaymentMade = 8,

    /// <summary>Reminder sent at the midpoint of an active cycle.</summary>
    CycleMidReminder = 9,

    /// <summary>Reminder sent 7 days before a cycle's end date.</summary>
    CycleClosingSoon = 10,

    /// <summary>An expense cycle has been closed by the admin.</summary>
    CycleClosed = 11,

    /// <summary>A member raised a dispute on an expense in a cycle.</summary>
    DisputeRaised = 12,

    /// <summary>The admin updated the status of an expense dispute.</summary>
    DisputeUpdated = 13,

    /// <summary>Admin manually nudged unsettled members showing their outstanding balance.</summary>
    ManualReminder = 14,

    /// <summary>A member voluntarily left a group; remaining members are notified.</summary>
    MemberLeftGroup = 15,

    /// <summary>A member accepted a group invite; existing members are notified.</summary>
    MemberJoinedGroup = 16,

    /// <summary>A user has been added to a Draft expense cycle.</summary>
    CycleMemberAdded = 17,

    /// <summary>A user has been removed from a Draft expense cycle.</summary>
    CycleMemberRemoved = 18,
}

