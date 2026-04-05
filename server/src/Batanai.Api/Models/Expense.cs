using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

public class Expense
{
    [Key]
    public int Id { get; set; }

    public int ExpenseCycleId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public decimal Amount { get; set; }

    [Required]
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

    /// <summary>The user who logged this expense (audit trail only). All members share the cost equally.</summary>
    public int LoggedByUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
