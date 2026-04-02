namespace Divvy.Api.Services;

/// <summary>
/// POCO bound to the "Vapid" section of appsettings.json.
/// </summary>
public class VapidSettings
{
    public string Subject { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}
