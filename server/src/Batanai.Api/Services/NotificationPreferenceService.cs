using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Notification;
using Batanai.Api.Models;

namespace Batanai.Api.Services;

/// <summary>
/// Manages per-user notification preferences.
/// Absence of a row in UserNotificationPreferences means the type is enabled (default-on).
/// </summary>
public class NotificationPreferenceService
{
    private readonly ApplicationDbContext _context;

    public NotificationPreferenceService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns all notification types with the user's current preference for each.
    /// Types with no stored preference default to IsEnabled = true.
    /// </summary>
    public async Task<IList<NotificationPreferenceDto>> GetPreferencesAsync(int userId)
    {
        var types = await _context.NotificationTypes
            .OrderBy(t => t.Id)
            .ToListAsync();

        var stored = await _context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToDictionaryAsync(p => p.NotificationTypeId, p => p.IsEnabled);

        return types.Select(t => new NotificationPreferenceDto
        {
            TypeId            = t.Id,
            TypeName          = t.Name,
            Description       = t.Description,
            IsEnabled         = stored.TryGetValue(t.Id, out var enabled) ? enabled : true,
            IsAdminControlled = t.IsAdminControlled
        }).ToList();
    }

    /// <summary>
    /// Upserts notification preferences for <paramref name="userId"/>.
    /// When <paramref name="isAdmin"/> is false, updates to admin-controlled types are silently skipped.
    /// </summary>
    public async Task UpdatePreferencesAsync(
        int userId,
        UpdateNotificationPreferencesRequest request,
        bool isAdmin)
    {
        if (request.Preferences == null || request.Preferences.Count == 0)
            return;

        // Build lookup: typeId ? IsAdminControlled
        var typeIds = request.Preferences.Select(p => p.TypeId).ToList();
        var typeMap = await _context.NotificationTypes
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.IsAdminControlled);

        // Load any existing preference rows for this user
        var existing = await _context.UserNotificationPreferences
            .Where(p => p.UserId == userId && typeIds.Contains(p.NotificationTypeId))
            .ToListAsync();
        var existingMap = existing.ToDictionary(p => p.NotificationTypeId);

        foreach (var item in request.Preferences)
        {
            // Skip if unknown type
            if (!typeMap.TryGetValue(item.TypeId, out var isAdminControlled))
                continue;

            // Non-admins cannot change admin-controlled types
            if (!isAdmin && isAdminControlled)
                continue;

            if (existingMap.TryGetValue(item.TypeId, out var pref))
            {
                pref.IsEnabled = item.IsEnabled;
            }
            else
            {
                _context.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    UserId             = userId,
                    NotificationTypeId = item.TypeId,
                    IsEnabled          = item.IsEnabled
                });
            }
        }

        await _context.SaveChangesAsync();
    }
}
