namespace Divvy.Api.DTOs.UserDevice;

public class UserDeviceDto
{
    public int Id { get; set; }
    public string ClientId { get; set; } = null!;
    public string DeviceType { get; set; } = null!;
    public string? DeviceModel { get; set; }
    public string? DeviceManufacturer { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? FriendlyName { get; set; }
    public string InstallStatus { get; set; } = null!;
    public DateTime? NextPromptAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
