namespace Batanai.Api.DTOs.UserDevice;

public class UpdateInstallStatusRequest
{
    /// <summary>InstallPromptStatus enum value: 0=Unknown, 1=RemindLater, 2=Deferred, 3=NeverAskAgain, 4=Installed</summary>
    public int InstallStatus { get; set; }
    public string ClientId { get; set; } = null!;
    public DateTime? NextPromptAt { get; set; }
}
