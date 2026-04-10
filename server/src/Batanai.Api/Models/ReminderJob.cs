using System.ComponentModel.DataAnnotations;

namespace Batanai.Api.Models;

public class ReminderJob
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public NotificationType NotificationType { get; set; }

    /// <summary>What entity this reminder is about, e.g. "Cycle", "KycCycle".</summary>
    [Required, MaxLength(50)]
    public string SubjectType { get; set; } = string.Empty;

    /// <summary>The PK of the subject entity.</summary>
    public int SubjectId { get; set; }

    /// <summary>Which milestone, e.g. "Initial", "Midpoint", "NearDue", "Overdue", "KycInitial".</summary>
    [Required, MaxLength(50)]
    public string StageKey { get; set; } = string.Empty;

    /// <summary>Unique key for idempotent insert, e.g. "PaymentDue:42:7:Initial".</summary>
    [Required, MaxLength(200)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime ScheduledForUtc { get; set; }

    public ReminderJobStatus Status { get; set; } = ReminderJobStatus.Pending;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Body { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? DeepLinkUrl { get; set; }

    public int? RelatedEntityId { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public int RetryCount { get; set; }

    [MaxLength(1000)]
    public string? LastError { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
