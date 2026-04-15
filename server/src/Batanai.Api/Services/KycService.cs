using Batanai.Api.Data;
using Batanai.Api.DTOs.Kyc;
using Batanai.Api.Models;
using Batanai.Api.Services.Reminders;
using Microsoft.EntityFrameworkCore;

namespace Batanai.Api.Services;

public class KycService
{
    private readonly ApplicationDbContext _context;
    private readonly IPushNotificationSender _push;
    private readonly ReminderSchedulingService _reminderScheduling;

    public KycService(ApplicationDbContext context, IPushNotificationSender push, ReminderSchedulingService reminderScheduling)
    {
        _context = context;
        _push = push;
        _reminderScheduling = reminderScheduling;
    }

    public async Task<(KycDocumentDto? dto, string? error)> SubmitKycAsync(int userId, SubmitKycRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return (null, "User not found.");

        if (string.IsNullOrWhiteSpace(request.FullNameOnId))
            return (null, "Full name on ID is required.");

        if (request.IdType != KycIdType.NoDocument)
        {
            if (string.IsNullOrWhiteSpace(request.IdNumber))
                return (null, "ID number is required.");
            if (request.DocumentFileId == null)
                return (null, "Identity document image is required.");
        }

        if (request.DocumentFileId.HasValue)
        {
            var docFile = await _context.UploadedFiles.FindAsync(request.DocumentFileId.Value);
            if (docFile == null) return (null, "Document file not found.");
            if (docFile.UploadedByUserId != userId) return (null, "Document file must belong to the current user.");
        }

        if (request.SelfieWithIdFileId.HasValue)
        {
            var selfieFile = await _context.UploadedFiles.FindAsync(request.SelfieWithIdFileId.Value);
            if (selfieFile == null) return (null, "Selfie file not found.");
            if (selfieFile.UploadedByUserId != userId) return (null, "Selfie file must belong to the current user.");
        }

        var entity = new UserKycDocument
        {
            UserId = userId,
            IdType = request.IdType,
            IdNumber = string.IsNullOrWhiteSpace(request.IdNumber) ? null : request.IdNumber.Trim(),
            FullNameOnId = request.FullNameOnId.Trim(),
            DocumentFileId = request.DocumentFileId,
            SelfieWithIdFileId = request.SelfieWithIdFileId,
            Status = request.IdType == KycIdType.NoDocument ? KycStatus.PendingReview : KycStatus.PendingReview,
            SubmittedAt = DateTime.UtcNow
        };

        _context.UserKycDocuments.Add(entity);
        user.KycStatus = KycStatus.PendingReview;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var adminIds = await _context.Users
            .Where(u => u.IsActive && (u.Role == Role.SuperAdmin || u.Role == Role.Admin))
            .Select(u => u.Id)
            .ToListAsync();

        if (adminIds.Count > 0)
        {
            await _push.SendToUsersAsync(
                adminIds,
                NotificationType.KycSubmitted,
                "KYC submitted for review",
                $"{user.FirstName} {user.LastName} submitted identity verification details.",
                "/admin/kyc",
                entity.Id);
        }

        return (Map(entity, user), null);
    }

    public async Task<List<KycDocumentDto>> GetPendingKycAsync()
    {
        var docs = await _context.UserKycDocuments
            .Include(d => d.User)
            .Where(d => d.Status == KycStatus.PendingReview)
            .OrderBy(d => d.SubmittedAt)
            .ToListAsync();

        return docs.Select(d => Map(d, d.User)).ToList();
    }

    public async Task<KycDocumentDto?> GetKycDocumentByUserIdAsync(int userId)
    {
        var doc = await _context.UserKycDocuments
            .Include(d => d.User)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.SubmittedAt)
            .FirstOrDefaultAsync();

        return doc == null ? null : Map(doc, doc.User);
    }

    public async Task<KycStatusDto?> GetKycStatusAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        var latest = await _context.UserKycDocuments
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.SubmittedAt)
            .FirstOrDefaultAsync();

        return new KycStatusDto
        {
            Status = user.KycStatus,
            RejectionReason = latest?.RejectionReason,
            SubmittedAt = latest?.SubmittedAt,
            ReviewedAt = latest?.ReviewedAt,
            CanResubmit = user.KycStatus == KycStatus.Rejected
        };
    }

    public async Task<string?> ReviewKycAsync(int kycId, int adminId, bool approve, string? reason)
    {
        var doc = await _context.UserKycDocuments
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == kycId);
        if (doc == null) return "KYC submission not found.";
        if (doc.User == null) return "User not found.";
        if (doc.Status != KycStatus.PendingReview) return "Only pending KYC submissions can be reviewed.";

        if (!approve && string.IsNullOrWhiteSpace(reason))
            return "A rejection reason is required.";

        doc.Status = approve ? KycStatus.Verified : KycStatus.Rejected;
        doc.ReviewedByUserId = adminId;
        doc.ReviewedAt = DateTime.UtcNow;
        doc.RejectionReason = approve ? null : reason?.Trim();
        doc.User.KycStatus = doc.Status;
        doc.User.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _push.SendToUserAsync(
            doc.UserId,
            approve ? NotificationType.KycApproved : NotificationType.KycRejected,
            approve ? "KYC approved" : "KYC rejected",
            approve
                ? "Your identity verification has been approved. You can now participate in Mukando cycles."
                : $"Your identity verification was rejected. {doc.RejectionReason}",
            "/profile",
            doc.Id);

        if (approve)
            await _reminderScheduling.CancelKycRemindersForUserAsync(doc.UserId);

        return null;
    }

    public async Task<string?> AdminBypassKycAsync(int targetUserId, int adminId, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return "A bypass note is required.";

        var user = await _context.Users.FindAsync(targetUserId);
        if (user == null) return "User not found.";

        var doc = new UserKycDocument
        {
            UserId = targetUserId,
            IdType = KycIdType.NoDocument,
            FullNameOnId = $"{user.FirstName} {user.LastName}",
            Status = KycStatus.AdminBypassed,
            SubmittedAt = DateTime.UtcNow,
            ReviewedByUserId = adminId,
            ReviewedAt = DateTime.UtcNow,
            AdminBypassNote = note.Trim()
        };

        _context.UserKycDocuments.Add(doc);
        user.KycStatus = KycStatus.AdminBypassed;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _push.SendToUserAsync(
            targetUserId,
            NotificationType.KycBypassed,
            "KYC manually approved",
            "An administrator has manually approved your identity verification so you can join Mukando cycles.",
            "/profile",
            doc.Id);

        await _reminderScheduling.CancelKycRemindersForUserAsync(targetUserId);

        return null;
    }

    private static KycDocumentDto Map(UserKycDocument doc, User? user) => new()
    {
        Id = doc.Id,
        UserId = doc.UserId,
        UserName = user == null ? "Unknown" : $"{user.FirstName} {user.LastName}",
        UserEmail = user?.Email ?? string.Empty,
        IdType = doc.IdType,
        IdNumber = doc.IdNumber,
        FullNameOnId = doc.FullNameOnId,
        DocumentFileUrl = doc.DocumentFileId.HasValue ? $"/api/files/{doc.DocumentFileId.Value}" : null,
        SelfieWithIdFileUrl = doc.SelfieWithIdFileId.HasValue ? $"/api/files/{doc.SelfieWithIdFileId.Value}" : null,
        Status = doc.Status,
        SubmittedAt = doc.SubmittedAt,
        RejectionReason = doc.RejectionReason,
        AdminBypassNote = doc.AdminBypassNote
    };
}
