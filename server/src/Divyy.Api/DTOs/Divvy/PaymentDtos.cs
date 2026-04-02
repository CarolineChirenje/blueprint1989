namespace Divvy.Api.DTOs.Divvy;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreatePaymentRequest(
    int PayeeId,
    int ExpenseCycleId,
    decimal Amount,
    string? Notes);

public record RespondPaymentRequest(bool Confirm, string? Notes);

// ── Responses ─────────────────────────────────────────────────────────────────

public record PaymentDto(
    int Id,
    int PayerId,
    string PayerFirstName,
    string PayerLastName,
    int PayeeId,
    string PayeeFirstName,
    string PayeeLastName,
    int ExpenseCycleId,
    decimal Amount,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    DateTime? ConfirmedAt);
