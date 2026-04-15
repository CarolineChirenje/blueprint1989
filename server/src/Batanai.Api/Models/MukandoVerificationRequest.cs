using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Batanai.Api.Models;

/// <summary>
/// Tracks a two-step verification request for a Mukando contribution confirmation
/// or payout. A randomly selected cycle participant is assigned as the verifier.
/// The verifier's identity is only surfaced after they respond to prevent collusion.
/// </summary>
public class MukandoVerificationRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MukandoRoundId { get; set; }

    /// <summary>Whether this verifies a contribution or a payout.</summary>
    public VerificationTarget Target { get; set; }

    /// <summary>FK to MukandoContribution — populated when Target = Contribution.</summary>
    public int? MukandoContributionId { get; set; }

    // ── Pending payout data (stored here until approved, Target = Payout only) ──

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PendingPayoutAmount { get; set; }

    public PaymentMethod? PendingPayoutMethod { get; set; }

    [StringLength(500)]
    public string? PendingPayoutProofUrl { get; set; }

    [StringLength(200)]
    public string? PendingPayoutReference { get; set; }

    // ── Participants ──────────────────────────────────────────────────────────

    /// <summary>The admin (or system) who initiated the action that triggered this verification.</summary>
    [Required]
    public int InitiatedByUserId { get; set; }

    /// <summary>The randomly selected cycle participant assigned to verify this action.</summary>
    [Required]
    public int AssignedToUserId { get; set; }

    // ── Status & Response ─────────────────────────────────────────────────────

    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;

    [StringLength(500)]
    public string? RejectionReason { get; set; }

    public int? RespondedByUserId { get; set; }

    public DateTime? RespondedAt { get; set; }

    // ── Timing ────────────────────────────────────────────────────────────────

    /// <summary>The verification expires after 48 hours; admin can then reassign.</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
