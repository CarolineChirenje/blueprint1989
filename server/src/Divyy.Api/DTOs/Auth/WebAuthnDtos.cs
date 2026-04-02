using System.ComponentModel.DataAnnotations;
using Fido2NetLib;

namespace Divvy.Api.DTOs.Auth;

// ─── Registration ─────────────────────────────────────────────────────────────

/// <summary>Wraps the browser's PublicKeyCredential attestation response together with an optional friendly name.</summary>
public class WebAuthnRegistrationCompleteRequest
{
    [Required]
    public AuthenticatorAttestationRawResponse Attestation { get; set; } = null!;

    /// <summary>User-supplied label, e.g. "My iPhone". Falls back to AAGUID lookup if omitted.</summary>
    [StringLength(100)]
    public string? FriendlyName { get; set; }
}

// ─── Authentication ───────────────────────────────────────────────────────────

public class WebAuthnAuthenticationBeginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

/// <summary>Wraps the browser's PublicKeyCredential assertion response together with the user's email.</summary>
public class WebAuthnAuthenticationCompleteRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public AuthenticatorAssertionRawResponse Assertion { get; set; } = null!;
}

// ─── Credential list ──────────────────────────────────────────────────────────

public class WebAuthnCredentialDto
{
    public int Id { get; set; }
    public string? DeviceFriendlyName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
