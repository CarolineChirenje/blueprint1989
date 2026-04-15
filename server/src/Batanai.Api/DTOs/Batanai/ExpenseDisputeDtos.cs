namespace Batanai.Api.DTOs.Batanai;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateDisputeRequest(
    int ExpenseId,
    string Reason);

public record UpdateDisputeStatusRequest(
    string Status,
    string? AdminNotes);

// ── Responses ─────────────────────────────────────────────────────────────────

public record ExpenseDisputeDto(
    int Id,
    int ExpenseId,
    string ExpenseTitle,
    int CycleId,
    int RaisedByUserId,
    string RaiserFullName,
    string Reason,
    string Status,
    string? AdminNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt);
