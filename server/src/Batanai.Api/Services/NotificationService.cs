using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.Models;
using Batanai.Api.DTOs.Notification;

namespace Batanai.Api.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(int userId, string message,
        NotificationType type = NotificationType.General,
        string? deepLinkUrl = null,
        int? relatedEntityId = null,
        bool sentViaPush = false)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Message = message,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            Type = type,
            DeepLinkUrl = deepLinkUrl,
            RelatedEntityId = relatedEntityId,
            SentViaPush = sentViaPush
        });
        await _context.SaveChangesAsync();
    }

    public async Task<NotificationSummaryDto> GetForUserAsync(
        int userId,
        bool unreadOnly = false,
        bool archivedOnly = false,
        int? take = null,
        int skip = 0)
    {
        var unreadCount = await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && !n.IsArchived);

        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        query = archivedOnly
            ? query.Where(n => n.IsArchived)
            : query.Where(n => !n.IsArchived);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        query = query.OrderByDescending(n => n.CreatedAt);

        if (skip > 0)
        {
            query = query.Skip(skip);
        }

        if (take.HasValue && take.Value > 0)
        {
            query = query.Take(take.Value);
        }

        var notifications = await query
            .Select(n => new NotificationDto(
                n.Id,
                n.Message,
                n.IsRead,
                n.CreatedAt,
                n.Type,
                n.DeepLinkUrl,
                n.RelatedEntityId,
                n.SentViaPush,
                n.IsArchived))
            .ToListAsync();

        return new NotificationSummaryDto(unreadCount, notifications);
    }

    public async Task<bool> MarkReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task MarkManyReadAsync(IReadOnlyCollection<int> notificationIds, int userId)
    {
        if (notificationIds.Count == 0)
        {
            return;
        }

        await _context.Notifications
            .Where(n => n.UserId == userId && notificationIds.Contains(n.Id) && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task MarkAllReadAsync(int userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && !n.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task ArchiveReadAsync(int userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && n.IsRead && !n.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsArchived, true));
    }

    public async Task<bool> RestoreAsync(int notificationId, int userId)
    {
        var updated = await _context.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId && n.IsArchived)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsArchived, false));

        return updated > 0;
    }

    public async Task<bool> ArchiveSingleAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId && !n.IsArchived);

        if (notification == null) return false;

        notification.IsArchived = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkUnreadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        notification.IsRead = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int notificationId, int userId)
    {
        var deleted = await _context.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId)
            .ExecuteDeleteAsync();

        return deleted > 0;
    }

    public async Task DeleteAllArchivedAsync(int userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && n.IsArchived)
            .ExecuteDeleteAsync();
    }
}
