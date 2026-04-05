using Batanai.Api.DTOs.Auth;
using Batanai.Api.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/webauthn")]
public class WebAuthnController : ControllerBase
{
    private readonly WebAuthnService _webAuthn;

    public WebAuthnController(WebAuthnService webAuthn)
    {
        _webAuthn = webAuthn;
    }

    // --- Registration ---------------------------------------------------------

    /// <summary>
    /// Begin WebAuthn credential registration for the currently logged-in user.
    /// Returns CredentialCreateOptions (challenge + RP details) for the browser.
    /// </summary>
    [HttpPost("registration/begin")]
    [Authorize]
    public async Task<IActionResult> RegistrationBegin()
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var options = await _webAuthn.BeginRegistrationAsync(userId);
            return Ok(options);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Complete WebAuthn credential registration.
    /// Verifies the authenticator attestation and saves the public key.
    /// </summary>
    [HttpPost("registration/complete")]
    [Authorize]
    public async Task<IActionResult> RegistrationComplete([FromBody] WebAuthnRegistrationCompleteRequest request, CancellationToken ct)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var credential = await _webAuthn.CompleteRegistrationAsync(
                userId,
                request.Attestation,
                request.FriendlyName,
                ct);

            return Ok(new
            {
                message      = "Biometric credential registered successfully.",
                credentialId = credential.CredentialId,
                friendlyName = credential.DeviceFriendlyName
            });
        }
        catch (Fido2VerificationException)
        {
            return BadRequest(new { message = "Credential verification failed." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // --- Authentication -------------------------------------------------------

    /// <summary>
    /// Begin WebAuthn authentication for a given email.
    /// Returns AssertionOptions (challenge + allowed credential IDs) for the browser.
    /// </summary>
    [HttpPost("authentication/begin")]
    [AllowAnonymous]
    public async Task<IActionResult> AuthenticationBegin([FromBody] WebAuthnAuthenticationBeginRequest request)
    {
        try
        {
            var options = await _webAuthn.BeginAuthenticationAsync(request.Email);
            return Ok(options);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Complete WebAuthn authentication.
    /// Verifies the assertion, updates the sign count, and returns a JWT.
    /// MFA is bypassed — biometric verification counts as both factors.
    /// </summary>
    [HttpPost("authentication/complete")]
    [AllowAnonymous]
    public async Task<IActionResult> AuthenticationComplete([FromBody] WebAuthnAuthenticationCompleteRequest request, CancellationToken ct)
    {
        try
        {
            var (user, token, expiresAt) = await _webAuthn.CompleteAuthenticationAsync(
                request.Email,
                request.Assertion,
                ct);

            return Ok(new
            {
                id           = user.Id,
                email        = user.Email,
                firstName    = user.FirstName,
                lastName     = user.LastName,
                role         = user.Role,
                token,
                tokenExpiresAt = expiresAt,
                isMfaRequired  = false,   // biometric bypasses MFA
                isMfaEnabled   = user.IsMfaEnabled,
                mfaEnabledAt   = user.MfaEnabledAt,
                passwordExpired = false
            });
        }
        catch (Fido2VerificationException)
        {
            return BadRequest(new { message = "Biometric verification failed." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // --- Credential management ------------------------------------------------

    /// <summary>List all biometric credentials registered by the current user.</summary>
    [HttpGet("credentials")]
    [Authorize]
    public async Task<IActionResult> GetCredentials()
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        var credentials = await _webAuthn.GetCredentialsAsync(userId);
        return Ok(credentials);
    }

    /// <summary>Delete a biometric credential belonging to the current user.</summary>
    [HttpDelete("credentials/{id:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteCredential(int id)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        var deleted = await _webAuthn.DeleteCredentialAsync(id, userId);
        if (!deleted)
            return NotFound(new { message = "Credential not found or does not belong to your account." });

        return Ok(new { message = "Biometric credential removed." });
    }
}
