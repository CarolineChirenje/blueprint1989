using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.Models;

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

    /// <summary>The user who paid this expense and is owed by the other members.</summary>
    public int PaidByUserId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
