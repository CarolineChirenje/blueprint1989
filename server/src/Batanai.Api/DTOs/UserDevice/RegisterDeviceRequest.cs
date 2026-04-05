namespace Batanai.Api.DTOs.UserDevice;

public class RegisterDeviceRequest
{
    public string ClientId { get; set; } = null!;
    public string? DeviceModel { get; set; }
    public string? DeviceManufacturer { get; set; }
    public string? OsVersion { get; set; }
    public string? AppVersion { get; set; }
    public string? UserAgent { get; set; }
}
