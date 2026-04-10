namespace Batanai.Api.Models;

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

    /// <summary>A feature/bug report status has been updated by an admin.</summary>
    FeatureBugReportResolved = 19,

    // -- Mukando --------------------------------------------------------------

    /// <summary>A Mukando round has started; all members are notified of the recipient and due date.</summary>
    MukandoRoundStarted = 20,

    /// <summary>A member submitted their contribution for a Mukando round.</summary>
    MukandoContributionReceived = 21,

    /// <summary>Admin confirmed a member's contribution for a Mukando round.</summary>
    MukandoContributionConfirmed = 22,

    /// <summary>All contributions for a Mukando round have been collected; admin is notified.</summary>
    MukandoAllContributionsCollected = 23,

    /// <summary>The payout for a Mukando round has been confirmed by the admin.</summary>
    MukandoPayoutConfirmed = 24,

    /// <summary>A Mukando round has been completed.</summary>
    MukandoRoundCompleted = 25,

    /// <summary>All rounds in a Mukando cycle are done.</summary>
    MukandoCycleCompleted = 26,

    /// <summary>A member requested to swap turns with another member.</summary>
    MukandoSwapRequested = 27,

    /// <summary>A swap request was accepted.</summary>
    MukandoSwapAccepted = 28,

    /// <summary>A swap request was declined.</summary>
    MukandoSwapDeclined = 29,

    /// <summary>Reminder: a Mukando contribution is due soon or overdue.</summary>
    MukandoContributionDue = 30,

    /// <summary>A member requested to opt out of a cycle.</summary>
    CycleOptOutRequested = 31,

    /// <summary>Admin responded to an opt-out request.</summary>
    CycleOptOutResponded = 32,

    // -- Join codes -----------------------------------------------------------

    /// <summary>A user requested to join a group via join code; group admins are notified.</summary>
    JoinRequestReceived = 33,

    /// <summary>A join request was approved by a group admin.</summary>
    JoinRequestApproved = 34,

    /// <summary>A join request was declined by a group admin.</summary>
    JoinRequestDeclined = 35,

    // -- Mukando Verification -------------------------------------------------

    /// <summary>A cycle participant has been randomly selected to verify a contribution or payout.</summary>
    MukandoVerificationRequested = 36,

    /// <summary>The randomly assigned verifier approved the contribution or payout.</summary>
    MukandoVerificationApproved = 37,

    /// <summary>The randomly assigned verifier rejected the contribution or payout.</summary>
    MukandoVerificationRejected = 38,

    /// <summary>The verifier was reassigned (previous assignee did not respond in time).</summary>
    MukandoVerifierReassigned = 39,

    // -- Cycle Agreement ------------------------------------------------------

    /// <summary>Agreements were reset because cycle settings, members, or opt-outs changed.</summary>
    CycleAgreementsReset = 40,

    /// <summary>All members have agreed and the cycle is ready to start.</summary>
    CycleAllMembersAgreed = 41,
}

