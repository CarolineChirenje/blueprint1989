using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Batanai.Api.Models;

public class MukandoContribution
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MukandoRoundId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public ContributionStatus Status { get; set; } = ContributionStatus.Pending;

    /// <summary>Proof of payment image URL (required when member marks as paid).</summary>
    [StringLength(500)]
    public string? ProofUrl { get; set; }

    /// <summary>Optional payment reference number.</summary>
    [StringLength(200)]
    public string? Reference { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? ConfirmedByAdminAt { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // ── Reminder tracking flags ──
    public bool Reminder1Sent { get; set; }
    public bool Reminder2Sent { get; set; }
    public bool EscalationSent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
