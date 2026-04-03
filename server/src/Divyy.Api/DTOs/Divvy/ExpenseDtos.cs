namespace Divvy.Api.DTOs.Divvy;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateExpenseRequest(
    int ExpenseCycleId,
    string Title,
    decimal Amount,
    string Category,
    string? Notes);

public record UpdateExpenseRequest(
    string Title,
    decimal Amount,
    string Category,
    string? Notes);

// ── Responses ─────────────────────────────────────────────────────────────────

public record ExpenseDto(
    int Id,
    int ExpenseCycleId,
    string Title,
    decimal Amount,
    string Category,
    int LoggedByUserId,
    string LoggedByName,
    string? Notes,
    DateTime CreatedAt,
    List<MemberObligationDto> Obligations);

public record MemberObligationDto(
    int Id,
    int ExpenseId,
    int UserId,
    string FirstName,
    string LastName,
    decimal AmountOwed,
    bool IsSettled,
    DateTime? SettledAt);

public record ObligationsSummaryDto(
    decimal TotalOwed,
    int UnsettledCount,
    int ActiveCycleCount,
    string? SoonestCycleName,
    DateTime? SoonestDueDate);
