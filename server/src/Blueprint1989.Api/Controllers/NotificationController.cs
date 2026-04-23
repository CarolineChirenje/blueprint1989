using Blueprint1989.Api.Services;
using Blueprint1989.Api.Models;
using Blueprint1989.Api.DTOs.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Blueprint1989.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly IPushNotificationSender _pushSender;

    public NotificationController(
        NotificationService notificationService,
        IPushNotificationSender pushSender)
    {
        _notificationService = notificationService;
        _pushSender = pushSender;
    }

    /// <summary>
    /// GET /api/notification
    /// Returns notifications (with unread count) for the current user.
    /// Supports compact bell use-cases via unread-only filtering, archived views, and result limiting.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] bool archivedOnly = false,
        [FromQuery] int? take = null,
        [FromQuery] int skip = 0)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var normalizedTake = take.HasValue && take.Value > 0
            ? Math.Min(take.Value, 100)
            : (int?)null;
        var normalizedSkip = Math.Max(skip, 0);

        var summary = await _notificationService.GetForUserAsync(userId, unreadOnly, archivedOnly, normalizedTake, normalizedSkip);
        return Ok(summary);
    }

    /// <summary>
    /// PUT /api/notification/{id}/read
    /// Marks a single notification as read.
    /// </summary>
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var success = await _notificationService.MarkReadAsync(id, userId);
        if (!success) return NotFound(new { message = "Notification not found." });

        return NoContent();
    }

    /// <summary>
    /// PUT /api/notification/read-batch
    /// Marks multiple notifications as read for the current user.
    /// </summary>
    [HttpPut("read-batch")]
    public async Task<IActionResult> MarkManyRead([FromBody] MarkNotificationsReadRequest request)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var ids = request.NotificationIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? [];

        if (ids.Count == 0)
            return NoContent();

        await _notificationService.MarkManyReadAsync(ids, userId);
        return NoContent();
    }

    /// <summary>
    /// PUT /api/notification/read-all
    /// Marks all notifications as read for the current user.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        await _notificationService.MarkAllReadAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// PUT /api/notification/archive-read
    /// Archives read notifications for the current user without deleting history.
    /// </summary>
    [HttpPut("archive-read")]
    public async Task<IActionResult> ArchiveRead()
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        await _notificationService.ArchiveReadAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// DELETE /api/notification/read
    /// Legacy alias that archives read notifications for the current user.
    /// </summary>
    [HttpDelete("read")]
    public async Task<IActionResult> ClearReadAlias()
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        await _notificationService.ArchiveReadAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// PUT /api/notification/{id}/restore
    /// Restores a previously archived notification.
    /// </summary>
    [HttpPut("{id:int}/restore")]
    public async Task<IActionResult> Restore(int id)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var success = await _notificationService.RestoreAsync(id, userId);
        if (!success) return NotFound(new { message = "Archived notification not found." });

        return NoContent();
    }

    /// <summary>
    /// PUT /api/notification/{id}/archive
    /// Archives a single notification.
    /// </summary>
    [HttpPut("{id:int}/archive")]
    public async Task<IActionResult> ArchiveSingle(int id)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var success = await _notificationService.ArchiveSingleAsync(id, userId);
        if (!success) return NotFound(new { message = "Notification not found or already archived." });

        return NoContent();
    }

    /// <summary>
    /// PUT /api/notification/{id}/unread
    /// Marks a single notification as unread.
    /// </summary>
    [HttpPut("{id:int}/unread")]
    public async Task<IActionResult> MarkUnread(int id)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var success = await _notificationService.MarkUnreadAsync(id, userId);
        if (!success) return NotFound(new { message = "Notification not found." });

        return NoContent();
    }

    /// <summary>
    /// DELETE /api/notification/archived
    /// Permanently deletes all archived notifications for the current user.
    /// </summary>
    [HttpDelete("archived")]
    public async Task<IActionResult> DeleteAllArchived()
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        await _notificationService.DeleteAllArchivedAsync(userId);
        return NoContent();
    }

    /// <summary>
    /// DELETE /api/notification/{id}
    /// Permanently deletes a single notification.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var success = await _notificationService.DeleteAsync(id, userId);
        if (!success) return NotFound(new { message = "Notification not found." });

        return NoContent();
    }

    /// <summary>
    /// POST /api/notification/ketone-alert
}
