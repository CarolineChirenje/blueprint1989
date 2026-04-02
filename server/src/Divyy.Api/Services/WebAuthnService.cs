using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Auth;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

public class WebAuthnService
{
    private readonly IFido2 _fido2;
    private readonly IMemoryCache _cache;
    private readonly ApplicationDbContext _context;
    private readonly AuthService _authService;

    private const string RegCachePrefix  = "webauthn_reg_";
    private const string AuthCachePrefix = "webauthn_auth_";

    public WebAuthnService(IFido2 fido2, IMemoryCache cache, ApplicationDbContext context, AuthService authService)
    {
        _fido2      = fido2;
        _cache      = cache;
        _context    = context;
        _authService = authService;
    }

    // ─── Registration ─────────────────────────────────────────────────────────

    /// <summary>Generate WebAuthn credential creation options and cache the challenge.</summary>
    public async Task<CredentialCreateOptions> BeginRegistrationAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var fido2User = new Fido2User
        {
            Id          = BitConverter.GetBytes(userId),
            Name        = user.Email,
            DisplayName = $"{user.FirstName} {user.LastName}"
        };

        // Exclude credentials already registered on the same device
        var existingKeys = await _context.WebAuthnCredentials
            .Where(c => c.UserId == userId)
            .Select(c => new PublicKeyCredentialDescriptor(FromBase64Url(c.CredentialId)))
            .ToListAsync();

        var authenticatorSelection = new AuthenticatorSelection
        {
            AuthenticatorAttachment = AuthenticatorAttachment.Platform, // device biometrics only
            UserVerification        = UserVerificationRequirement.Required,
            RequireResidentKey      = false
        };

        var options = _fido2.RequestNewCredential(
            fido2User,
            existingKeys,
            authenticatorSelection,
            AttestationConveyancePreference.None);

        _cache.Set($"{RegCachePrefix}{userId}", options, TimeSpan.FromMinutes(5));
        return options;
    }

    /// <summary>Verify the browser's attestation and persist the new credential.</summary>
    public async Task<WebAuthnCredential> CompleteRegistrationAsync(
        int userId,
        AuthenticatorAttestationRawResponse attestationResponse,
        string? friendlyName,
        CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue($"{RegCachePrefix}{userId}", out CredentialCreateOptions? storedOptions) || storedOptions is null)
            throw new InvalidOperationException("Registration session expired or not found. Please try again.");

        _cache.Remove($"{RegCachePrefix}{userId}");

        async Task<bool> IsCredentialUnique(IsCredentialIdUniqueToUserParams args, CancellationToken ct)
        {
            var encoded = ToBase64Url(args.CredentialId);
            return !await _context.WebAuthnCredentials.AnyAsync(c => c.CredentialId == encoded, ct);
        }

        var result = await _fido2.MakeNewCredentialAsync(
            attestationResponse,
            storedOptions,
            IsCredentialUnique,
            cancellationToken: cancellationToken);

        var reg = result.Result
            ?? throw new Fido2VerificationException("Credential registration returned no result.");

        var credential = new WebAuthnCredential
        {
            UserId             = userId,
            CredentialId      = ToBase64Url(reg.CredentialId),
            PublicKey         = ToBase64Url(reg.PublicKey),
            SignCount         = reg.Counter,
            Aaguid            = null,   // not exposed in Fido2 v3.0.1 CredentialMakeResult
            DeviceFriendlyName = string.IsNullOrWhiteSpace(friendlyName)
                                    ? "Biometric Authenticator"
                                    : friendlyName.Trim(),
            CreatedAt         = DateTime.UtcNow
        };

        _context.WebAuthnCredentials.Add(credential);
        await _context.SaveChangesAsync(cancellationToken);
        return credential;
    }

    // ─── Authentication ───────────────────────────────────────────────────────

    /// <summary>Generate WebAuthn assertion options and cache the challenge.</summary>
    public async Task<AssertionOptions> BeginAuthenticationAsync(string email)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive)
            ?? throw new InvalidOperationException("No active account found with that email.");

        var credentials = await _context.WebAuthnCredentials
            .Where(c => c.UserId == user.Id)
            .ToListAsync();

        if (credentials.Count == 0)
            throw new InvalidOperationException("No biometric credentials registered for this account. Please set one up in your profile.");

        var allowList = credentials
            .Select(c => new PublicKeyCredentialDescriptor(FromBase64Url(c.CredentialId)))
            .ToList();

        var options = _fido2.GetAssertionOptions(allowList, UserVerificationRequirement.Required);

        _cache.Set($"{AuthCachePrefix}{email.ToLowerInvariant()}", options, TimeSpan.FromMinutes(5));
        return options;
    }

    /// <summary>
    /// Verify the browser's assertion, update the sign count, and return a JWT.
    /// MFA is NOT required for biometric-authenticated sessions.
    /// </summary>
    public async Task<(User user, string token, DateTime expiresAt)> CompleteAuthenticationAsync(
        string email,
        AuthenticatorAssertionRawResponse assertionResponse,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{AuthCachePrefix}{email.ToLowerInvariant()}";

        if (!_cache.TryGetValue(cacheKey, out AssertionOptions? storedOptions) || storedOptions is null)
            throw new InvalidOperationException("Authentication session expired or not found. Please try again.");

        _cache.Remove(cacheKey);

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var credentialIdEncoded = ToBase64Url(assertionResponse.Id);
        var storedCred = await _context.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialIdEncoded && c.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Credential not recognised or does not belong to this account.");

        async Task<bool> IsUserHandleOwner(IsUserHandleOwnerOfCredentialIdParams args, CancellationToken ct)
        {
            var userIdFromHandle = BitConverter.ToInt32(args.UserHandle, 0);
            return await _context.WebAuthnCredentials.AnyAsync(
                c => c.CredentialId == ToBase64Url(args.CredentialId) && c.UserId == userIdFromHandle, ct);
        }

        var result = await _fido2.MakeAssertionAsync(
            assertionResponse,
            storedOptions,
            FromBase64Url(storedCred.PublicKey),
            (uint)storedCred.SignCount,
            IsUserHandleOwner,
            cancellationToken: cancellationToken);

        // Update sign-count (prevents replay attacks with cloned authenticators)
        storedCred.SignCount  = result.Counter;
        storedCred.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = _authService.GenerateJwtToken(user);
        return (user, token, expiresAt);
    }

    // ─── Credential management ────────────────────────────────────────────────

    public async Task<List<WebAuthnCredentialDto>> GetCredentialsAsync(int userId) =>
        await _context.WebAuthnCredentials
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new WebAuthnCredentialDto
            {
                Id               = c.Id,
                DeviceFriendlyName = c.DeviceFriendlyName,
                CreatedAt        = c.CreatedAt,
                LastUsedAt       = c.LastUsedAt
            })
            .ToListAsync();

    public async Task<bool> DeleteCredentialAsync(int credentialId, int userId)
    {
        var cred = await _context.WebAuthnCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.UserId == userId);

        if (cred is null) return false;

        _context.WebAuthnCredentials.Remove(cred);
        await _context.SaveChangesAsync();
        return true;
    }

    // ─── Encoding helpers ─────────────────────────────────────────────────────

    private static string ToBase64Url(byte[] data) =>
        Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] FromBase64Url(string base64url)
    {
        var padded = base64url.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };
        return Convert.FromBase64String(padded);
    }

}
