using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

/// <summary>Allows a cycle member to flag an expense item before the cycle starts.</summary>
public class ExpenseDispute
{
    [Key]
    public int Id { get; set; }

    public int ExpenseId { get; set; }

    public int RaisedByUserId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    public DisputeStatus Status { get; set; } = DisputeStatus.Pending;

    [StringLength(1000)]
    public string? AdminNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
