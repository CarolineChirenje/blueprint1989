using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

/// <summary>
/// Records each non-payer member's share of a single expense.
/// Created automatically when an expense is added or updated.
/// </summary>
public class MemberObligation
{
    [Key]
    public int Id { get; set; }

    public int ExpenseId { get; set; }

    /// <summary>The member who owes this share (never the payer of the expense).</summary>
    public int UserId { get; set; }

    [Required]
    public decimal AmountOwed { get; set; }

    public bool IsSettled { get; set; } = false;

    public DateTime? SettledAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
