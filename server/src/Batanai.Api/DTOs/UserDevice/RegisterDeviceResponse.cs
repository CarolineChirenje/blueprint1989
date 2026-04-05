namespace Batanai.Api.DTOs.UserDevice;

public class RegisterDeviceResponse
{
    public int Id { get; set; }
    public string ClientId { get; set; } = null!;
    public string InstallStatus { get; set; } = null!;
    public DateTime? NextPromptAt { get; set; }
    public bool IsNew { get; set; }
}
