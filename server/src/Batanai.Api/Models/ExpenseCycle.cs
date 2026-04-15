using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Batanai.Api.Models;

public class ExpenseCycle
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public CycleStatus Status { get; set; } = CycleStatus.Draft;

    /// <summary>How expenses are split across members.</summary>
    public SplitType SplitType { get; set; } = SplitType.Equal;

    /// <summary>Majana (expense sharing) or Mukando (round-robin payout).</summary>
    public CycleType CycleType { get; set; } = CycleType.Majana;

    /// <summary>Fixed contribution per round (Mukando only).</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ContributionAmount { get; set; }

    /// <summary>How often rounds occur (Mukando only).</summary>
    public CycleFrequency? Frequency { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Sent once when the cycle is started (Active).</summary>
    public bool StartNotificationSent { get; set; } = false;

    /// <summary>Sent once at the midpoint of the cycle duration.</summary>
    public bool MidReminderSent { get; set; } = false;

    /// <summary>Sent once 7 days before EndDate.</summary>
    public bool ClosingSoonSent { get; set; } = false;

    /// <summary>Group this cycle belongs to. Every cycle must belong to a group.</summary>
    [Required]
    public int GroupId { get; set; }

    /// <summary>Currency for all monetary values in this cycle.</summary>
    [Required]
    public int CurrencyId { get; set; }
}
