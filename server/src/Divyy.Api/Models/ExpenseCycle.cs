using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.Models;

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
}
