namespace Divvy.Api.Models;

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
    Pending  = 1,
    Accepted = 2,
    Declined = 3
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
