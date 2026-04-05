using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.Models;

public class MukandoSwapRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ExpenseCycleId { get; set; }

    [Required]
    public int RequesterUserId { get; set; }

    [Required]
    public int RequesterRoundId { get; set; }

    [Required]
    public int TargetUserId { get; set; }

    [Required]
    public int TargetRoundId { get; set; }

    public SwapRequestStatus Status { get; set; } = SwapRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }
}
