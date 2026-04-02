namespace Divvy.Api.Models;

public enum InstallPromptStatus
{
    Unknown = 0,
    RemindLater = 1,
    Deferred = 2,
    NeverAskAgain = 3,
    Installed = 4
}

public enum DeviceType
{
    Unknown = 0,
    Android = 1,
    iOS = 2,
    Desktop = 3
}

public class UserDevice
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Stable UUID generated per browser/device, stored in localStorage.</summary>
    public string ClientId { get; set; } = null!;

    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public string? DeviceModel { get; set; }
    public string? DeviceManufacturer { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Optional user-supplied friendly label, e.g. "My Work Laptop".</summary>
    public string? FriendlyName { get; set; }

    public InstallPromptStatus InstallStatus { get; set; } = InstallPromptStatus.Unknown;

    /// <summary>When RemindLater is set, the next time to show the install prompt.</summary>
    public DateTime? NextPromptAt { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
