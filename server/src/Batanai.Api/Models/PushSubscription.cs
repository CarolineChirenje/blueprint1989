namespace Batanai.Api.Models;

/// <summary>
/// Stores a browser's Web Push subscription per user per device.
/// Cascade-deleted when the owning User is deleted.
/// </summary>
public class PushSubscription
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Push service endpoint URL provided by the browser.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Browser's ECDH public key (base64url).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Authentication secret (base64url).</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Optional user-agent string to help identify the device.</summary>
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
