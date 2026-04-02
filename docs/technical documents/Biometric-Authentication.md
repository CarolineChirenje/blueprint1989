# Biometric Authentication (WebAuthn / FIDO2)

## Overview

Vitara supports passwordless login via the Web Authentication API (WebAuthn), enabling users on compatible devices to authenticate with biometrics such as Touch ID, Face ID, or Windows Hello. Once a credential is registered, the user can log in by typing their email address and tapping a biometric button — no password is entered and MFA is bypassed. Credentials are stored server-side as FIDO2 `WebAuthnCredential` records and managed from the Profile → Security (Biometric) page.

---

## Role Access

All authenticated roles can register and manage biometric credentials. The feature is conditionally shown only when the browser reports `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable() === true`.

---

## Backend

### Controller: `WebAuthnController` — `/api/webauthn`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/webauthn/registration/begin` | Authorized | Returns a FIDO2 `CredentialCreateOptions` challenge |
| POST | `/api/webauthn/registration/complete` | Authorized | Verifies attestation and saves the credential |
| POST | `/api/webauthn/authentication/begin` | Anonymous | Returns `AssertionOptions` challenge by email |
| POST | `/api/webauthn/authentication/complete` | Anonymous | Verifies assertion, updates `SignCount`, returns JWT |
| GET | `/api/webauthn/credentials` | Authorized | Lists all credentials for the current user |
| DELETE | `/api/webauthn/credentials/{id}` | Authorized | Deletes a specific credential (ownership enforced) |

### Key DTOs

**`WebAuthnRegistrationCompleteRequest`**
```
attestationResponse   string    JSON-serialized AuthenticatorAttestationResponse
friendlyName          string?   Optional human-readable name for the credential
```

**`WebAuthnAuthenticationBeginRequest`**
```
email    string    The user's email address
```

**`WebAuthnAuthenticationCompleteRequest`**
```
email                  string    Must match the email used in authentication/begin
assertionResponse      string    JSON-serialized AuthenticatorAssertionResponse
```

### Service: `WebAuthnService`

**Dependencies:** `IFido2` (singleton, `Fido2NetLib`), `ApplicationDbContext`, `IMemoryCache`, `AuthService`

#### Registration Flow
1. **`/registration/begin`:** Builds `CredentialCreateOptions` from `Fido2NetLib` using the user's id and email as `Fido2User`. Options are cached in `IMemoryCache` keyed to the authenticated `userId` with a short TTL. Returns the options JSON to the client.
2. **`/registration/complete`:** Retrieves cached options. Calls `Fido2.MakeNewCredentialAsync()` to verify the attestation. Checks whether a credential with the same `CredentialId` already exists for this user (blocks duplicates). Saves a new `WebAuthnCredential` row with the `CredentialId` (base64url), `PublicKey` (COSE encoding), `SignCount`, optional `Aaguid`, `DeviceFriendlyName`, and optional `UserDeviceId` link.

#### Authentication Flow
1. **`/authentication/begin`:** Looks up the user by email, loads all their stored `WebAuthnCredential` rows, builds `AssertionOptions` from `Fido2NetLib` with the credential IDs as `allowCredentials`. Caches options keyed to `userId`. Returns options JSON to the client.
2. **`/authentication/complete`:** Retrieves cached options. Calls `Fido2.MakeAssertionAsync()` to verify the assertion. Updates `SignCount` on the matched `WebAuthnCredential` record (replay attack detection). Calls `AuthService.GenerateJwtToken()` directly — **MFA is not required** for biometric assertion. Returns a full `AuthResponse` with a valid JWT.

### Model: `WebAuthnCredential`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User |
| `CredentialId` | string | Base64url-encoded credential ID |
| `PublicKey` | byte[] | COSE-encoded public key |
| `SignCount` | uint | Incremented counter for replay protection |
| `Aaguid` | string? | Authenticator AAGUID (device model hint) |
| `DeviceFriendlyName` | string? | User-assigned label |
| `UserDeviceId` | int? | FK → UserDevice (optional) |
| `CreatedAt` | DateTime | |
| `LastUsedAt` | DateTime? | Updated on each successful assertion |

### Security Properties

- **Attestation verification:** `Fido2NetLib` verifies the authenticator's attestation statement against the RP (relying party) origin. In production, the RP ID is derived from the configured domain.
- **Sign count enforcement:** The `SignCount` must be greater than the stored value on each assertion; a decrease indicates a cloned authenticator and the library rejects the request.
- **Ownership check on delete:** `/credentials/{id}` verifies that the credential's `UserId` matches the authenticated user before deleting.
- **No MFA step:** Biometric assertion is treated as a second factor in itself; the TOTP challenge is not presented after a successful assertion.
- **Options TTL:** `CredentialCreateOptions` and `AssertionOptions` are cached in `IMemoryCache` for a short period. Completing a ceremony after the TTL expires results in a 400 error.

---

## Frontend

### Route

| Path | Component | Module |
|---|---|---|
| `/profile/biometric` | `BiometricSetupComponent` | `ProfileModule` |

### `BiometricSetupComponent` — `/profile/biometric`

**File:** `client/src/app/features/profile/components/biometric-setup.component.ts`

**On init:**
1. Calls `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()`. If `false`, shows an unsupported-device message and disables all controls.
2. Loads registered credentials via `GET /webauthn/credentials`.

**Registering a new credential:**
1. User optionally enters a friendly name for the credential.
2. Calls `BiometricService.registerCredential(friendlyName?)`.
3. Internally:
   - `POST /webauthn/registration/begin` → returns `CredentialCreateOptions`.
   - Browser `navigator.credentials.create({ publicKey: options })` → triggers OS biometric prompt.
   - `POST /webauthn/registration/complete` with the attestation response.
4. On success, refreshes the credential list.
5. Stores `bgl_biometric_email` in `localStorage` with the current user's email — this hint causes the `LoginComponent` to display the biometric button when the same email is typed.

**Deleting a credential:**
1. User clicks the trash icon next to a credential.
2. Calls `DELETE /webauthn/credentials/{id}`.
3. Refreshes the list.
4. If no credentials remain, clears the `bgl_biometric_email` localStorage hint so the login page reverts to standard password-only mode.

**Credential list display:**
- Shows each credential with its friendly name (or "Unknown Device" if none), AAGUID hint, registration date, and last-used date.
- "Current device" badge if the credential's `UserDeviceId` matches the current session's device.

### Core Service: `BiometricService`

**File:** `client/src/app/core/services/biometric.service.ts`

**`registerCredential(friendlyName?)`**
```
1. POST /webauthn/registration/begin
   → receives CredentialCreateOptions JSON
2. navigator.credentials.create({ publicKey: parsedOptions })
   → browser triggers OS biometric prompt
   → returns AuthenticatorAttestationResponse
3. POST /webauthn/registration/complete
   body: { attestationResponse: serialized, friendlyName }
   → returns saved WebAuthnCredential DTO
```

**`authenticate(email)`**
```
1. POST /webauthn/authentication/begin
   body: { email }
   → receives AssertionOptions JSON
2. navigator.credentials.get({ publicKey: parsedOptions })
   → browser triggers OS biometric prompt
   → returns AuthenticatorAssertionResponse
3. POST /webauthn/authentication/complete
   body: { email, assertionResponse: serialized }
   → returns AuthResponse with JWT
```

**`isAvailable()`**
- Returns `Promise<boolean>` wrapping `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()`.

**`hasStoredCredentialHint(email)`**
- Returns `true` if `localStorage.getItem('bgl_biometric_email') === email`.

---

## Integration with Login Flow

When a user navigates to `/login` and types an email address:
1. `LoginComponent` calls `BiometricService.hasStoredCredentialHint(email)` and `BiometricService.isAvailable()`.
2. If both are true, a biometric icon button appears next to the password field.
3. Clicking the button calls `BiometricService.authenticate(email)`.
4. On success, the returned JWT is stored by `AuthService` (identical to standard login completion).
5. `PushNotificationService.subscribeToServer()` is called.
6. The user is navigated to `/dashboard`.

The password field is never submitted in this flow — an empty string is passed to `navigator.credentials.get()` as the `userHandle`, and the server identifies the user by stored `CredentialId` lookup, not by password.

---

## End-to-End Data Flow

### Credential Registration
```
Browser (OS Prompt)      Angular                     API                   DB
  |                      |-- POST /register/begin -->|                     |
  |                      |<-- CredentialCreateOptions|                     |
  |<-- biometric prompt--|                            |                     |
  |-- sign + attestation->|                          |                     |
  |                      |-- POST /register/complete->|                     |
  |                      |                           |-- INSERT credential->|
  |                      |<-- credential DTO --------|                     |
  | set localStorage hint|                            |                     |
```

### Biometric Login
```
Browser (OS Prompt)      Angular                     API                   DB
  |                      |-- POST /authenticate/begin->|                   |
  |                      |<-- AssertionOptions --------|                   |
  |<-- biometric prompt--|                              |                   |
  |-- signed assertion -->|                             |                   |
  |                      |-- POST /authenticate/complete>|                 |
  |                      |                             |-- verify assertion |
  |                      |                             |-- update SignCount |
  |                      |                             |-- generate JWT     |
  |                      |<-- JWT (AuthResponse) -------|                  |
  | store token          |                             |                   |
  |<-- navigate /dashboard                             |                   |
```
