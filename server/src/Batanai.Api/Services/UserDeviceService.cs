using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.Models;
using Batanai.Api.DTOs.UserDevice;

namespace Batanai.Api.Services;

public class UserDeviceService
{
    private readonly ApplicationDbContext _context;

    public UserDeviceService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Register a new device or update LastSeenAt for an existing one.
    /// Returns the device state so the client knows whether to show the install prompt.
    /// </summary>
    public async Task<RegisterDeviceResponse> RegisterOrUpdateAsync(int userId, RegisterDeviceRequest request)
    {
        var existing = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.ClientId == request.ClientId);

        bool isNew = existing == null;

        if (existing == null)
        {
            existing = new UserDevice
            {
                UserId = userId,
                ClientId = request.ClientId,
                DeviceType = DetectDeviceType(request.UserAgent),
                DeviceModel = request.DeviceModel,
                DeviceManufacturer = request.DeviceManufacturer,
                OsVersion = request.OsVersion,
                AppVersion = request.AppVersion,
                UserAgent = request.UserAgent,
                InstallStatus = InstallPromptStatus.Unknown,
                IsActive = true,
                LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _context.UserDevices.Add(existing);
        }
        else
        {
            // Update mutable fields on each app launch
            existing.LastSeenAt = DateTime.UtcNow;
            existing.IsActive = true;
            if (request.AppVersion != null) existing.AppVersion = request.AppVersion;
            if (request.UserAgent != null) existing.UserAgent = request.UserAgent;
            if (request.OsVersion != null) existing.OsVersion = request.OsVersion;
        }

        await _context.SaveChangesAsync();

        return new RegisterDeviceResponse
        {
            Id = existing.Id,
            ClientId = existing.ClientId,
            InstallStatus = existing.InstallStatus.ToString(),
            NextPromptAt = existing.NextPromptAt,
            IsNew = isNew
        };
    }

    /// <summary>Update the PWA install prompt status for a specific device.</summary>
    public async Task<bool> UpdateInstallStatusAsync(int userId, UpdateInstallStatusRequest request)
    {
        var device = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.ClientId == request.ClientId);

        if (device == null) return false;

        device.InstallStatus = (InstallPromptStatus)request.InstallStatus;
        device.NextPromptAt = request.NextPromptAt;
        device.LastSeenAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>List all active devices for the signed-in user.</summary>
    public async Task<List<UserDeviceDto>> GetDevicesAsync(int userId)
    {
        return await _context.UserDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderByDescending(d => d.LastSeenAt)
            .Select(d => new UserDeviceDto
            {
                Id = d.Id,
                ClientId = d.ClientId,
                DeviceType = d.DeviceType.ToString(),
                DeviceModel = d.DeviceModel,
                DeviceManufacturer = d.DeviceManufacturer,
                OsVersion = d.OsVersion,
                AppVersion = d.AppVersion,
                FriendlyName = d.FriendlyName,
                InstallStatus = d.InstallStatus.ToString(),
                NextPromptAt = d.NextPromptAt,
                IsActive = d.IsActive,
                LastSeenAt = d.LastSeenAt,
                CreatedAt = d.CreatedAt
            })
            .ToListAsync();
    }

    /// <summary>Update the friendly (display) name for a device owned by the given user.</summary>
    public async Task<bool> RenameDeviceAsync(int userId, int deviceId, string friendlyName)
    {
        var device = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Id == deviceId && d.IsActive);

        if (device == null) return false;

        device.FriendlyName = friendlyName.Trim();
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>Soft-delete (deactivate) a device by its Id for the signed-in user.</summary>
    public async Task<bool> DeleteDeviceAsync(int userId, int deviceId)
    {
        var device = await _context.UserDevices
            .FirstOrDefaultAsync(d => d.UserId == userId && d.Id == deviceId);

        if (device == null) return false;

        device.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    // -- Helpers --------------------------------------------------------------

    private static DeviceType DetectDeviceType(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return DeviceType.Unknown;
        var ua = userAgent.ToLower();
        if (ua.Contains("android")) return DeviceType.Android;
        if (ua.Contains("iphone") || ua.Contains("ipad") || ua.Contains("ipod")) return DeviceType.iOS;
        return DeviceType.Desktop;
    }
}
