using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.DTOs.Notification;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

/// <summary>
/// Endpoints for reading and updating per-user notification preferences.
///
/// User routes:
///   GET  /api/notification-preferences          — get own preferences
///   PUT  /api/notification-preferences          — update own preferences (admin-controlled types ignored)
///
/// Admin routes:
///   GET  /api/admin/notification-preferences/{userId}   — get any user's preferences
///   PUT  /api/admin/notification-preferences/{userId}   — update any type for any user
/// </summary>
[ApiController]
[Authorize]
public class NotificationPreferenceController : ControllerBase
{
    private readonly NotificationPreferenceService _service;

    public NotificationPreferenceController(NotificationPreferenceService service)
    {
        _service = service;
    }

    // -- User: own preferences -----------------------------------------------

    [HttpGet("api/notification-preferences")]
    public async Task<IActionResult> GetOwn()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Invalid user token" });

        var prefs = await _service.GetPreferencesAsync(userId.Value);
        return Ok(prefs);
    }

    [HttpPut("api/notification-preferences")]
    public async Task<IActionResult> UpdateOwn([FromBody] UpdateNotificationPreferencesRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Invalid user token" });

        await _service.UpdatePreferencesAsync(userId.Value, request, isAdmin: false);
        return NoContent();
    }

    // -- Admin: any user's preferences --------------------------------------

    [HttpGet("api/admin/notification-preferences/{userId:int}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> GetForUser(int userId)
    {
        var prefs = await _service.GetPreferencesAsync(userId);
        return Ok(prefs);
    }

    [HttpPut("api/admin/notification-preferences/{userId:int}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> UpdateForUser(int userId, [FromBody] UpdateNotificationPreferencesRequest request)
    {
        await _service.UpdatePreferencesAsync(userId, request, isAdmin: true);
        return NoContent();
    }

    // -- Helpers -------------------------------------------------------------

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}
