namespace Divvy.Api.DTOs.Divvy;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateExpenseCycleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    List<int>? MemberUserIds,
    int GroupId);

public record UpdateExpenseCycleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate);

// ── Responses ─────────────────────────────────────────────────────────────────

public record ExpenseCycleDto(
    int Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string SplitType,
    int CreatedByUserId,
    DateTime CreatedAt,
    List<CycleMemberDto> Members,
    /// <summary>"GroupAdmin" or "GroupMember" — role of the requesting user in this cycle's group.</summary>
    string CurrentUserGroupRole);

public record ExpenseCycleSummaryDto(
    int Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    int MemberCount,
    int ExpenseCount,
    decimal TotalAmount,
    DateTime CreatedAt,
    int GroupId,
    string GroupName);

public record CycleMemberDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email);

public record CycleBalanceDto(
    int CycleId,
    string CycleName,
    List<MemberBalanceDto> Balances);

public record MemberBalanceDto(
    int UserId,
    string FirstName,
    string LastName,
    /// <summary>Positive = this user owes the current user. Negative = current user owes this user.</summary>
    decimal NetBalance);

// ── Contribution Summary ──────────────────────────────────────────────────────

public record CycleMemberContributionDto(
    int UserId,
    string FirstName,
    string LastName,
    decimal ShareOwed,
    decimal TotalPaid,
    /// <summary>Positive = overpaid, Negative = still owes.</summary>
    decimal Balance,
    bool IsSettled);

public record CycleContributionSummaryDto(
    int CycleId,
    string CycleName,
    decimal TotalExpenses,
    int MemberCount,
    decimal SharePerMember,
    List<CycleMemberContributionDto> Members,
    /// <summary>Sum of absolute balances for all unsettled members.</summary>
    decimal TotalOutstanding);

// ── Outstanding Summary (cross-cycle) ────────────────────────────────────────

public record OutstandingSummaryDto(
    decimal TotalOutstanding,
    int CycleCount,
    List<CycleOutstandingItemDto> Cycles);

public record CycleOutstandingItemDto(
    int CycleId,
    string CycleName,
    decimal Outstanding,
    decimal SharePerMember,
    decimal TotalPaid);
