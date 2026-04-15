using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

/// <summary>
/// A payment submitted by one member (payer) to another (payee) to settle debts within a cycle.
/// Must be confirmed by the payee before it is considered settled.
/// </summary>
public class Payment
{
    [Key]
    public int Id { get; set; }

    /// <summary>The user sending money.</summary>
    public int PayerId { get; set; }

    /// <summary>The user receiving money.</summary>
    public int PayeeId { get; set; }

    public int ExpenseCycleId { get; set; }

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ConfirmedAt { get; set; }
}
