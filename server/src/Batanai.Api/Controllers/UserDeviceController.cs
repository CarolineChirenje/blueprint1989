using Batanai.Api.DTOs.UserDevice;
using Batanai.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/me/devices")]
[Authorize]
public class UserDeviceController : ControllerBase
{
    private readonly UserDeviceService _deviceService;

    public UserDeviceController(UserDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    /// <summary>
    /// POST /api/me/devices/register
    /// Register this browser/device or refresh its LastSeenAt. Returns current install status.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDeviceRequest request)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(new { message = "ClientId is required." });

        var response = await _deviceService.RegisterOrUpdateAsync(userId, request);
        return Ok(response);
    }

    /// <summary>
    /// PATCH /api/me/devices/install-status
    /// Update the PWA install prompt status for a specific device.
    /// </summary>
    [HttpPatch("install-status")]
    public async Task<IActionResult> UpdateInstallStatus([FromBody] UpdateInstallStatusRequest request)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(new { message = "ClientId is required." });

        var success = await _deviceService.UpdateInstallStatusAsync(userId, request);
        if (!success) return NotFound(new { message = "Device not found." });

        return NoContent();
    }

    /// <summary>
    /// GET /api/me/devices
    /// List all active devices for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDevices()
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var devices = await _deviceService.GetDevicesAsync(userId);
        return Ok(devices);
    }

    /// <summary>
    /// PATCH /api/me/devices/{deviceId}/rename
    /// Set a friendly display name for a device.
    /// </summary>
    [HttpPatch("{deviceId:int}/rename")]
    public async Task<IActionResult> RenameDevice(int deviceId, [FromBody] RenameDeviceRequest request)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        if (string.IsNullOrWhiteSpace(request.FriendlyName))
            return BadRequest(new { message = "Friendly name cannot be empty." });

        var success = await _deviceService.RenameDeviceAsync(userId, deviceId, request.FriendlyName);
        if (!success) return NotFound(new { message = "Device not found." });

        return NoContent();
    }

    /// <summary>
    /// DELETE /api/me/devices/{deviceId}
    /// Deactivate (soft-delete) a device for the current user.
    /// </summary>
    [HttpDelete("{deviceId:int}")]
    public async Task<IActionResult> DeleteDevice(int deviceId)
    {
        var userIdClaim = User.FindFirst("id");
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized(new { message = "Invalid user token" });

        var success = await _deviceService.DeleteDeviceAsync(userId, deviceId);
        if (!success) return NotFound(new { message = "Device not found." });

        return NoContent();
    }
}
