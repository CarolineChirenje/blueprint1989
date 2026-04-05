using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Divvy.Api.Models;

public class MukandoRound
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ExpenseCycleId { get; set; }

    public int RoundNumber { get; set; }

    [Required]
    public int RecipientUserId { get; set; }

    public RoundStatus Status { get; set; } = RoundStatus.Pending;

    /// <summary>ContributionAmount × (MemberCount − 1). The recipient doesn't pay.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedPool { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCollected { get; set; }

    public bool PayoutConfirmed { get; set; }

    public DateTime? PayoutConfirmedAt { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
