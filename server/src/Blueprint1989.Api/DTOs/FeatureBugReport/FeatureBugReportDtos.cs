using Blueprint1989.Api.Models;

namespace Blueprint1989.Api.DTOs.FeatureBugReport;

public record CreateFeatureBugReportRequest(
    string Title,
    string Description,
    ReportType Type,
    ReportPriority Priority,
    List<ReportCategory> Categories
);

public record UpdateFeatureBugReportRequest(
    string Title,
    string Description,
    ReportType Type,
    ReportPriority Priority,
    List<ReportCategory> Categories
);

public record UpdateReportStatusRequest(
    ReportStatus Status,
    string? VersionNumber = null
);

public record FeatureBugReportResponseDto(
    int Id,
    string Title,
    string Description,
    ReportType Type,
    string TypeName,
    ReportPriority Priority,
    string PriorityName,
    ReportStatus Status,
    string StatusName,
    List<ReportCategory> Categories,
    List<string> CategoryNames,
    string? VersionNumber,
    int SubmittedByUserId,
    string SubmittedByName,
    DateTime SubmittedAt,
    DateTime UpdatedAt,
    string? ImageUrl
);
