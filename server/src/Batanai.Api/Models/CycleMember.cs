namespace Batanai.Api.Models;

/// <summary>Join table: which users belong to a given ExpenseCycle.</summary>
public class CycleMember
{
    public int ExpenseCycleId { get; set; }

    public int UserId { get; set; }

    /// <summary>Null = equal share. Populated only when SplitType=Custom (future feature).</summary>
    public decimal? SharePercentage { get; set; }

    /// <summary>Participant (default) or Observer. Observers receive all notifications and can raise disputes/opt-out, but have no financial obligations.</summary>
    public CycleRole CycleRole { get; set; } = CycleRole.Participant;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
