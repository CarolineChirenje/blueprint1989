namespace Divvy.Api.DTOs.Divvy;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateExpenseCycleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    List<int> MemberUserIds,
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
    int CreatedByUserId,
    DateTime CreatedAt,
    List<CycleMemberDto> Members);

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
