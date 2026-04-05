using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Divvy.Api.Models;

public class MukandoPayout
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MukandoRoundId { get; set; }

    [Required]
    public int RecipientUserId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountDisbursed { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>Proof of payout image URL (required).</summary>
    [Required]
    [StringLength(500)]
    public string ProofUrl { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Reference { get; set; }

    /// <summary>Admin who confirmed the payout.</summary>
    [Required]
    public int ConfirmedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
