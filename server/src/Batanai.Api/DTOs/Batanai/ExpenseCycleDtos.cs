namespace Batanai.Api.DTOs.Batanai;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateExpenseCycleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    List<int>? MemberUserIds,
    int GroupId,
    int CurrencyId,
    /// <summary>Majana or Mukando. Defaults to Majana.</summary>
    string CycleType = "Majana",
    /// <summary>Fixed contribution per round (Mukando only, required).</summary>
    decimal? ContributionAmount = null,
    /// <summary>Weekly, Biweekly, or Monthly (Mukando only, required).</summary>
    string? Frequency = null,
    /// <summary>Ordered list of member user IDs defining payout sequence (Mukando only, required).</summary>
    List<int>? PayoutOrder = null,
    int? CopyExpensesFromCycleId = null);

public record UpdateExpenseCycleRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate);

public record UpdateMukandoSettingsRequest(
    decimal ContributionAmount,
    string Frequency,
    List<int> PayoutOrder);

public record DuplicateCycleRequest(DateTime NewStartDate);

public record AddMembersBatchRequest(List<int> UserIds);

// ── Responses ─────────────────────────────────────────────────────────────────

public record ExpenseCycleDto(
    int Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string SplitType,
    string CycleType,
    int CreatedByUserId,
    DateTime CreatedAt,
    List<CycleMemberDto> Members,
    /// <summary>"GroupAdmin" or "GroupMember" — role of the requesting user in this cycle's group.</summary>
    string CurrentUserGroupRole,
    int GroupId,
    int CurrencyId,
    string CurrencyCode,
    string CurrencySymbol,
    /// <summary>Mukando only.</summary>
    decimal? ContributionAmount,
    /// <summary>Mukando only.</summary>
    string? Frequency);

public record ExpenseCycleSummaryDto(
    int Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string CycleType,
    int MemberCount,
    int ExpenseCount,
    decimal TotalAmount,
    DateTime CreatedAt,
    int GroupId,
    string GroupName,
    /// <summary>"GroupAdmin" or "GroupMember" — role of the requesting user in this cycle's group.</summary>
    string CurrentUserGroupRole,
    string CurrencyCode,
    string CurrencySymbol);

public record CycleMemberDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string GroupRole);

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

// ── Currency ─────────────────────────────────────────────────────────────────

public record CurrencyDto(
    int Id,
    string Code,
    string Name,
    string Symbol);

// ── Mukando Rounds ───────────────────────────────────────────────────────────

public record MukandoRoundDto(
    int Id,
    int RoundNumber,
    int RecipientUserId,
    string RecipientName,
    string Status,
    decimal ExpectedPool,
    decimal ActualCollected,
    DateTime DueDate,
    bool PayoutConfirmed,
    List<MukandoContributionDto>? Contributions);

public record MukandoContributionDto(
    int Id,
    int UserId,
    string FirstName,
    string LastName,
    decimal Amount,
    string Status,
    string? ProofUrl,
    string? Reference,
    DateTime? PaidAt,
    DateTime? ConfirmedByAdminAt);

public record MukandoPayoutDto(
    int Id,
    int RecipientUserId,
    string RecipientName,
    decimal AmountDisbursed,
    string PaymentMethod,
    string ProofUrl,
    string? Reference,
    int ConfirmedByUserId,
    DateTime CreatedAt);

public record RecordContributionRequest(
    string ProofUrl = "",
    string? Reference = null,
    string? Notes = null);

public record RecordPayoutRequest(
    decimal AmountDisbursed = 0,
    string PaymentMethod = "",
    string ProofUrl = "",
    string? Reference = null);

// ── Mukando Summary ──────────────────────────────────────────────────────────

public record MukandoCycleSummaryDto(
    int CycleId,
    string CycleName,
    decimal TotalDisbursed,
    decimal TotalCollected,
    decimal TotalExpectedPool,
    int RoundsCompleted,
    int TotalRounds,
    decimal OnTimeContributionRate,
    List<MemberReliabilityDto> MemberReliability);

public record MemberReliabilityDto(
    int UserId,
    string FirstName,
    string LastName,
    int OnTimeCount,
    int MissedCount,
    int TotalRoundsParticipated,
    decimal ReliabilityPercent);

// ── Mukando Activity Log ─────────────────────────────────────────────────────

public record MukandoRoundActivityDto(
    int Id,
    string Action,
    string Details,
    int UserId,
    string UserName,
    DateTime CreatedAt);

// ── Mukando Swap Requests ────────────────────────────────────────────────────

public record CreateSwapRequest(int TargetUserId);

public record RespondSwapRequest(bool Accept);

public record MukandoSwapRequestDto(
    int Id,
    int RequesterUserId,
    string RequesterName,
    int RequesterRoundNumber,
    int TargetUserId,
    string TargetName,
    int TargetRoundNumber,
    string Status,
    DateTime CreatedAt,
    DateTime? RespondedAt);

// ── Cycle Opt-out ────────────────────────────────────────────────────────────

public record CreateOptOutRequest(string Reason);

public record RespondOptOutRequest(bool Approve);

public record OptOutRequestDto(
    int Id,
    int UserId,
    string UserName,
    string Reason,
    string Status,
    DateTime CreatedAt,
    DateTime? RespondedAt);

// ── Dashboard Mukando Extension ──────────────────────────────────────────────

public record MukandoDashboardDto(
    MukandoNextContributionDto? NextContribution,
    MukandoPayoutRoundDto? YourPayoutRound,
    MukandoActiveRoundStatusDto? ActiveRoundStatus);

public record MukandoNextContributionDto(
    int CycleId,
    string CycleName,
    decimal Amount,
    string CurrencySymbol,
    DateTime DueDate,
    int RoundNumber,
    string RecipientName);

public record MukandoPayoutRoundDto(
    int CycleId,
    string CycleName,
    int RoundNumber,
    DateTime EstimatedDate);

public record MukandoActiveRoundStatusDto(
    int CycleId,
    string CycleName,
    int RoundNumber,
    int ContributionsCollected,
    int TotalExpected);

// ── Mukando Verification ──────────────────────────────────────────────────────

public record RespondToVerificationRequest(
    bool Approve,
    string? RejectionReason = null);

public record MukandoVerificationRequestDto(
    int Id,
    int MukandoRoundId,
    string Target,
    string Status,
    /// <summary>Only visible after the verifier has responded — blank before then.</summary>
    int AssignedToUserId,
    string AssignedToName,
    DateTime ExpiresAt,
    DateTime CreatedAt,
    int? ContributionId,
    string? ContributorName,
    decimal? ContributionAmount,
    string? RejectionReason);
