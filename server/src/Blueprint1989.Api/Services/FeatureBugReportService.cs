using Microsoft.EntityFrameworkCore;
using Blueprint1989.Api.Data;
using Blueprint1989.Api.Models;
using Blueprint1989.Api.DTOs.FeatureBugReport;

namespace Blueprint1989.Api.Services;

public class FeatureBugReportService
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationSender _push;
    private readonly IFileStorageService _fileStorage;

    public FeatureBugReportService(ApplicationDbContext context, IPushNotificationSender push, IFileStorageService fileStorage)
    {
        _context = context;
        _push = push;
        _fileStorage = fileStorage;
    }

    // ── Queries ────────────────────────────────────────────────────────────

    public async Task<List<FeatureBugReportResponseDto>> GetFilteredAsync(
        ReportStatus? status = null, ReportType? type = null, ReportPriority? priority = null)
    {
        var query = _context.FeatureBugReports
            .Include(r => r.SubmittedByUser)
            .Include(r => r.Categories)
            .AsQueryable();

        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        if (type.HasValue) query = query.Where(r => r.Type == type.Value);
        if (priority.HasValue) query = query.Where(r => r.Priority == priority.Value);

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToDto(r))
            .ToListAsync();
    }

    public async Task<FeatureBugReportResponseDto?> GetByIdAsync(int id)
    {
        var report = await _context.FeatureBugReports
            .Include(r => r.SubmittedByUser)
            .Include(r => r.Categories)
            .FirstOrDefaultAsync(r => r.Id == id);

        return report == null ? null : ToDto(report);
    }

    public async Task<List<FeatureBugReportResponseDto>> GetMyReportsAsync(int userId)
    {
        return await _context.FeatureBugReports
            .Include(r => r.SubmittedByUser)
            .Include(r => r.Categories)
            .Where(r => r.SubmittedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToDto(r))
            .ToListAsync();
    }

    // ── Commands ───────────────────────────────────────────────────────────

    public async Task<FeatureBugReportResponseDto> CreateAsync(int userId, CreateFeatureBugReportRequest request, IFormFile? image = null)
    {
        int? imageFileId = null;
        if (image != null)
        {
            var (fileId, _) = await _fileStorage.UploadAsync(image, "reports", userId);
            imageFileId = fileId;
        }

        var report = new Models.FeatureBugReport
        {
            Title = request.Title,
            Description = request.Description,
            Type = request.Type,
            Priority = request.Priority,
            Status = ReportStatus.New,
            SubmittedByUserId = userId,
            ImageFileId = imageFileId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.FeatureBugReports.Add(report);
        await _context.SaveChangesAsync();

        // Add categories
        if (request.Categories is { Count: > 0 })
        {
            foreach (var cat in request.Categories.Distinct())
            {
                _context.FeatureBugReportCategories.Add(new FeatureBugReportCategory
                {
                    FeatureBugReportId = report.Id,
                    Category = cat
                });
            }
            await _context.SaveChangesAsync();
        }

        return (await GetByIdAsync(report.Id))!;
    }

    public async Task<FeatureBugReportResponseDto?> UpdateAsync(int id, int userId, UpdateFeatureBugReportRequest request)
    {
        var report = await _context.FeatureBugReports
            .Include(r => r.Categories)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return null;
        if (report.SubmittedByUserId != userId) return null;
        if (report.Status != ReportStatus.New) return null;

        report.Title = request.Title;
        report.Description = request.Description;
        report.Type = request.Type;
        report.Priority = request.Priority;
        report.UpdatedAt = DateTime.UtcNow;

        // Replace categories
        _context.FeatureBugReportCategories.RemoveRange(report.Categories);
        foreach (var cat in request.Categories.Distinct())
        {
            _context.FeatureBugReportCategories.Add(new FeatureBugReportCategory
            {
                FeatureBugReportId = report.Id,
                Category = cat
            });
        }

        await _context.SaveChangesAsync();
        return (await GetByIdAsync(report.Id))!;
    }

    public async Task<FeatureBugReportResponseDto?> UpdateStatusAsync(int id, UpdateReportStatusRequest request)
    {
        var report = await _context.FeatureBugReports
            .Include(r => r.SubmittedByUser)
            .Include(r => r.Categories)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (report == null) return null;

        report.Status = request.Status;
        report.UpdatedAt = DateTime.UtcNow;

        if (request.Status == ReportStatus.Closed && !string.IsNullOrWhiteSpace(request.VersionNumber))
        {
            report.VersionNumber = request.VersionNumber;
        }

        await _context.SaveChangesAsync();

        // Notify the submitter
        var statusLabel = request.Status.ToString();
        var message = request.Status == ReportStatus.Closed
            ? $"Your report \"{report.Title}\" has been closed in version {report.VersionNumber}."
            : $"Your report \"{report.Title}\" status changed to {statusLabel}.";

        await _push.SendToUserAsync(
            report.SubmittedByUserId,
            NotificationType.FeatureBugReportResolved,
            "Report Update",
            message,
            deepLinkUrl: "/profile/my-reports",
            relatedEntityId: report.Id);

        return ToDto(report);
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var report = await _context.FeatureBugReports.FindAsync(id);
        if (report == null) return false;
        if (report.SubmittedByUserId != userId) return false;
        if (report.Status == ReportStatus.InReview) return false;

        _context.FeatureBugReports.Remove(report);
        await _context.SaveChangesAsync();
        return true;
    }

    // ── Mapping ────────────────────────────────────────────────────────────

    private static FeatureBugReportResponseDto ToDto(Models.FeatureBugReport r) => new(
        r.Id,
        r.Title,
        r.Description,
        r.Type,
        r.Type.ToString(),
        r.Priority,
        r.Priority.ToString(),
        r.Status,
        r.Status.ToString(),
        r.Categories.Select(c => c.Category).ToList(),
        r.Categories.Select(c => c.Category.ToString()).ToList(),
        r.VersionNumber,
        r.SubmittedByUserId,
        $"{r.SubmittedByUser.FirstName} {r.SubmittedByUser.LastName}",
        r.CreatedAt,  // maps to SubmittedAt in DTO
        r.UpdatedAt,
        r.ImageFileId.HasValue ? $"/api/files/{r.ImageFileId.Value}" : null
    );
}
