using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.Models;

public class MukandoOptOutRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ExpenseCycleId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    public OptOutRequestStatus Status { get; set; } = OptOutRequestStatus.Pending;

    public int? RespondedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }
}
