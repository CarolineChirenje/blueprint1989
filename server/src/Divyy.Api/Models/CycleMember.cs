namespace Divvy.Api.Models;

/// <summary>Join table: which users belong to a given ExpenseCycle.</summary>
public class CycleMember
{
    public int ExpenseCycleId { get; set; }

    public int UserId { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
