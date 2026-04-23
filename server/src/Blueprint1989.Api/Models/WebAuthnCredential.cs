using System.ComponentModel.DataAnnotations;

namespace Blueprint1989.Api.Models;

/// <summary>
/// Stores a WebAuthn (FIDO2) public-key credential registered by a user on a device.
/// Each row represents one biometric authenticator (fingerprint, Face ID, Windows Hello, etc.)
/// </summary>
public class WebAuthnCredential
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    /// <summary>Base64Url-encoded credential ID assigned by the authenticator.</summary>
    [Required]
    [StringLength(512)]
    public string CredentialId { get; set; } = string.Empty;

    /// <summary>COSE-encoded public key, stored as Base64Url string.</summary>
    [Required]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Authenticator signature counter — incremented on each assertion; used to detect cloned authenticators.</summary>
    public long SignCount { get; set; } = 0;

    /// <summary>Authenticator AAGUID — identifies the make/model of the authenticator.</summary>
    [StringLength(100)]
    public string? Aaguid { get; set; }

    /// <summary>User-supplied or auto-detected friendly device name (e.g., "iPhone 15 Pro", "Windows Hello").</summary>
    [StringLength(100)]
    public string? DeviceFriendlyName { get; set; }

    /// <summary>Optional FK to UserDevices — links this credential to a tracked device record.</summary>
    public int? UserDeviceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual UserDevice? UserDevice { get; set; }
}
