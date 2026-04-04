using System.ComponentModel.DataAnnotations;

namespace Divvy.Api.Models;

// ── Enums ──────────────────────────────────────────────────────────────────

public enum ReportType
{
    Feature = 1,
    Bug     = 2
}

public enum ReportPriority
{
    Critical = 1,
    High     = 2,
    Medium   = 3,
    Low      = 4
}

public enum ReportStatus
{
    New      = 1,
    InReview = 2,
    Closed   = 3
}

public enum ReportCategory
{
    UI          = 1,
    Backend     = 2,
    Performance = 3,
    Security    = 4,
    API         = 5,
    Mobile      = 6,
    Desktop     = 7,
    Other       = 8
}

// ── Entities ───────────────────────────────────────────────────────────────

public class FeatureBugReport
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ReportType Type { get; set; }

    [Required]
    public ReportPriority Priority { get; set; }

    [Required]
    public ReportStatus Status { get; set; } = ReportStatus.New;

    [MaxLength(20)]
    public string? VersionNumber { get; set; }

    public int SubmittedByUserId { get; set; }
    public User SubmittedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation – many-to-many categories
    public ICollection<FeatureBugReportCategory> Categories { get; set; } = new List<FeatureBugReportCategory>();
}

/// <summary>Join table for multi-select categories on a report.</summary>
public class FeatureBugReportCategory
{
    public int FeatureBugReportId { get; set; }
    public FeatureBugReport Report { get; set; } = null!;

    public ReportCategory Category { get; set; }
}
