namespace Batanai.Api.Models;

public enum CycleStatus
{
    Draft  = 0,
    Active = 1,
    Closed = 2
}

public enum ExpenseCategory
{
    Rent          = 1,
    Utilities     = 2,
    Groceries     = 3,
    Transport     = 4,
    Entertainment = 5,
    Other         = 6
}

public enum PaymentStatus
{
    Pending   = 1,
    Confirmed = 2,
    Rejected  = 3
}

public enum GroupRole
{
    GroupAdmin  = 1,
    GroupMember = 2
}

public enum GroupInviteStatus
{
    Pending       = 1,
    Accepted      = 2,
    Declined      = 3,
    JoinRequested = 4
}

public enum SplitType
{
    Equal  = 1,
    Custom = 2
}

public enum DisputeStatus
{
    Pending  = 1,
    Reviewed = 2,
    Resolved = 3,
    Rejected = 4
}

public enum CycleType
{
    Majana  = 0,
    Mukando = 1
}

public enum CycleFrequency
{
    Weekly   = 1,
    Biweekly = 2,
    Monthly  = 3
}

public enum RoundStatus
{
    Pending   = 0,
    Active    = 1,
    Completed = 2
}

public enum ContributionStatus
{
    Pending              = 0,
    Paid                 = 1,
    Confirmed            = 2,
    Missed               = 3,
    AwaitingVerification = 4
}

public enum SwapRequestStatus
{
    Pending   = 0,
    Accepted  = 1,
    Declined  = 2,
    Cancelled = 3
}

public enum OptOutRequestStatus
{
    Pending  = 0,
    Approved = 1,
    Rejected = 2
}

public enum PaymentMethod
{
    Cash          = 1,
    BankTransfer  = 2,
    MobileMoney   = 3,
    Other         = 4
}

public enum RoundActivityAction
{
    RoundActivated                      = 1,
    ContributionSubmitted               = 2,
    ContributionConfirmed               = 3,
    PayoutRecorded                      = 4,
    RoundForceClose                     = 5,
    SwapRequested                       = 6,
    SwapAccepted                        = 7,
    SwapDeclined                        = 8,
    OptOutRequested                     = 9,
    OptOutApproved                      = 10,
    OptOutRejected                      = 11,
    ContributionVerificationRequested   = 12,
    ContributionVerificationApproved    = 13,
    ContributionVerificationRejected    = 14,
    PayoutVerificationRequested         = 15,
    PayoutVerificationApproved          = 16,
    PayoutVerificationRejected          = 17,
    VerificationReassigned              = 18
}

public enum VerificationTarget
{
    Contribution = 1,
    Payout       = 2
}

public enum VerificationStatus
{
    Pending    = 0,
    Approved   = 1,
    Rejected   = 2,
    Reassigned = 3
}

public enum KycStatus
{
    NotStarted    = 0,
    PendingReview = 1,
    Verified      = 2,
    Rejected      = 3,
    AdminBypassed = 4
}

public enum ReminderJobStatus
{
    Pending   = 0,
    Sent      = 1,
    Skipped   = 2,
    Cancelled = 3,
    Failed    = 4
}

public enum KycIdType
{
    NationalId      = 0,
    Passport        = 1,
    DriversLicense  = 2,
    NoDocument      = 3
}
