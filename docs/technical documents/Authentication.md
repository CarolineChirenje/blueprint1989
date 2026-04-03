# Authentication

## Overview

The authentication feature handles all aspects of user identity in Divvy, including account registration, email/password login, multi-factor authentication (TOTP), password expiry enforcement, forgot/reset password flows, and JWT issuance. Every request to a protected API endpoint is authorised via a JWT Bearer token. The JWT embeds the user's `id`, `email`, `role`, `firstName`, and `lastName` claims.

---

## Roles

| ID | Name | Description |
|---|---|---|
| 1 | SuperAdmin | Full system access |
| 2 | Admin | Manages users, cycles, config |
| 3 | Member | Standard user; joins expense cycles |

---

## Backend

### Controller: `AuthController` â€” `/api/auth`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/signup` | Anonymous | Register a new user account |
| POST | `/api/auth/login` | Anonymous | Authenticate with email + password |
| POST | `/api/auth/verify-mfa` | Anonymous | Verify TOTP code after primary login |
| POST | `/api/auth/change-expired-password` | Anonymous | Change password when it has expired |
| POST | `/api/auth/skip-mfa-setup` | Anonymous | Record that the user deferred MFA setup |
| POST | `/api/auth/mfa/setup` | Authorized | Generate TOTP secret + QR code + backup codes |
| POST | `/api/auth/mfa/confirm` | Authorized | Confirm MFA setup by verifying TOTP code |
| POST | `/api/auth/mfa/disable` | Authorized | Disable MFA, clear secret and backup codes |
| PUT | `/api/auth/profile` | Authorized | Update first/last name and/or change password |
| POST | `/api/auth/forgot-password` | Anonymous | Send password-reset email |
| GET | `/api/auth/validate-reset-token` | Anonymous | Validate a password-reset token |
| POST | `/api/auth/reset-password` | Anonymous | Set a new password using a reset token |

### Key DTOs

**`SignupRequest`**
```
email            string       Required
firstName        string       Required
lastName         string       Required
password         string       Min 8 chars, upper + lower + digit
role             int          2=Admin, 3=Member
adminPin         string?      Required for Admin role
```

**`LoginRequest`**
```
email    string
password string
```

**`AuthResponse`**
```
token             string    JWT Bearer token
requiresMfa       bool      True if TOTP verification step is required next
mfaEnabled        bool      Whether the account has MFA active
isPasswordExpired bool      True if password has passed PasswordExpirationDays threshold
userId            int       For use in change-expired-password flow
```

**`MfaSetupResponse`**
```
secret      string    Base32 TOTP secret
qrCode      string    Base64-encoded PNG QR code image
backupCodes string[]  10 single-use recovery codes
```

**`UpdateProfileRequest`**
```
firstName       string?
lastName        string?
currentPassword string?   Required when changing password
newPassword     string?   Min 8 chars, upper + lower + digit
```

### Service: `AuthService`

**Dependencies:** `ApplicationDbContext`, `IConfiguration`, `MfaService`, `IPasswordHashingService`, `AppConfigService`, `IEmailService`

#### `LoginByEmailAsync`
1. Looks up user by email (case-insensitive).
2. Verifies `PasswordHash` via `IPasswordHashingService`.
3. Checks `PasswordExpirationDays` from `AppConfigService`; if elapsed since `PasswordLastChanged`, sets `isPasswordExpired = true` â€” no JWT issued, returns userId for change-expired-password flow.
4. If user has `IsMfaEnabled = true`, returns `requiresMfa = true` with no full token; client must call `/verify-mfa`.
5. On success, calls `GenerateJwtToken` and returns `AuthResponse`.

#### `GenerateJwtToken`
- Reads `JwtExpirationMinutes` from `AppConfigService`.
- Creates claims: `id`, `email`, `role` (name), `firstName`, `lastName`.
- Signs with HMAC-SHA256 using `JWT_KEY` environment variable.

#### `SignupAsync`
1. Validates email uniqueness (case-insensitive), password strength (min 8 chars, at least one uppercase, one lowercase, one digit).
2. Creates `User` entity with hashed password.
3. If `role == Admin`: validates `adminPin` against `AdminSignupPin` in `AppConfigService`.
4. Seeds all `UserNotificationPreference` rows with `IsEnabled = true`.
5. Returns JWT via `GenerateJwtToken`.

#### `EnableMfaAsync` / `ConfirmMfaAsync` / `DisableMfaAsync`
- `EnableMfaAsync`: Generates TOTP secret via `MfaService`, generates 10 backup codes as JSON, stores temporarily (not yet saved until `ConfirmMfaAsync`).
- `ConfirmMfaAsync`: Verifies the submitted TOTP code, then persists `MfaSecret`, `BackupCodesJson`, and sets `IsMfaEnabled = true`.
- `DisableMfaAsync`: Clears `MfaSecret`, `BackupCodesJson`, sets `IsMfaEnabled = false`.

#### `ForgotPasswordAsync` / `ResetPasswordAsync`
- `ForgotPasswordAsync`: Looks up email, generates a time-limited `PasswordResetToken`, applies rate limiting (`PasswordResetRequestLimitPerHour` from AppConfig), sends HTML email via `IEmailService`.
- `ResetPasswordAsync`: Validates token (not expired per `PasswordResetTokenValidityMinutes`), hashes new password, saves, marks token used.

### Service: `MfaService`

- Generates Base32 TOTP secrets.
- Builds Google Charts QR code URL (used for authenticator app scanning).
- Verifies 6-digit TOTP codes via a TOTP library with standard 30-second window.
- Generates and validates single-use backup codes.

### Model: `User`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `Email` | string (255) | Unique, case-insensitive |
| `FirstName` | string | |
| `LastName` | string | |
| `PasswordHash` | string | BCrypt or PBKDF2 |
| `PasswordLastChanged` | DateTime? | Null = never changed |
| `Role` | enum (1–3) | |
| `IsActive` | bool | |
| `IsMfaEnabled` | bool | |
| `MfaSecret` | string? | Base32 TOTP secret |
| `MfaEnabledAt` | DateTime? | |
| `BackupCodesJson` | string? | JSON array of backup codes |
| `CreatedAt` | DateTime | |
| `UpdatedAt` | DateTime | |

### Model: `PasswordResetToken`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK â†’ User |
| `Token` | string | Secure random token |
| `ExpiresAt` | DateTime | From `PasswordResetTokenValidityMinutes` |
| `UsedAt` | DateTime? | Null = still valid |
| `CreatedAt` | DateTime | |

### Business Logic & Validation Rules

- **Password strength:** minimum 8 characters, at least one uppercase letter, one lowercase letter, one digit. Enforced on signup, reset, and profile change.
- **Email uniqueness:** checked case-insensitively against the `Users` table before any insert.
- **Password expiry:** `PasswordExpirationDays` from `AppConfig`. If `PasswordLastChanged` (or `CreatedAt` if null) is more than this many days ago, login is blocked and the change-expired-password flow is triggered.
- **Admin PIN:** required when registering with `role = Admin`. Compared against `AdminSignupPin` AppConfig key.
- **Rate limiting on password reset:** limited to `PasswordResetRequestLimitPerHour` requests per email per hour. No-reveal design â€” identical response returned for known and unknown emails.

---

## Frontend

### Routes

| Path | Component | Auth Required |
|---|---|---|
| `/login` | `LoginComponent` | No |
| `/signup` | `SignupComponent` | No |
| `/forgot-password` | `ForgotPasswordComponent` | No |
| `/reset-password` | `ResetPasswordComponent` | No |
| `/change-expired-password` | `ChangeExpiredPasswordComponent` | No |

All auth routes are in `AuthModule` (`client/src/app/auth/auth.module.ts`).

### `LoginComponent` â€” `/login`

**File:** `client/src/app/auth/login.component.ts`

**Behaviour:**
1. Displays an email + password form.
2. If the user has previously registered a biometric credential (`localStorage` key `bgl_biometric_email` matches the typed email) AND the browser has a platform authenticator available (`PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()`), a biometric login button appears and the password field becomes optional.
3. On standard login, calls `POST /auth/login`:
   - If `isPasswordExpired` is returned â†’ navigates to `/change-expired-password?userId=â€¦`.
   - If `requiresMfa` is returned â†’ reveals the inline MFA step with a 6-digit TOTP code input.
4. **MFA step:** calls `POST /auth/verify-mfa` with the code; on success stores the JWT via `AuthService`.
5. On successful login (either path), calls `PushNotificationService.subscribeToServer()` to register/refresh the browser push subscription.
6. "Remember Me" checkbox persists login state.
7. Redirects already-authenticated users to `/dashboard` on init.

**Service calls:**
- `AuthService.login(email, password)`
- `AuthService.verifyMfa(code)`
- `BiometricService.authenticate(email)` â€” triggers OS biometric prompt
- `PushNotificationService.subscribeToServer()`

### `SignupComponent` â€” `/signup`

**File:** `client/src/app/auth/signup.component.ts`

**Behaviour:**
- Multi-section reactive form that adapts based on the selected role.
- **All roles:** first name, last name, email, password (with strength indicator).
- **Admin:** admin PIN field.
- Password strength enforced client-side: minimum 8 chars, uppercase, lowercase, digit.
- Calls `POST /auth/signup` and navigates to `/login` on success.

### `ForgotPasswordComponent` â€” `/forgot-password`

**File:** `client/src/app/auth/forgot-password.component.ts`

- Single email input field.
- Calls `POST /auth/forgot-password`.
- Displays the same success message regardless of whether the email exists (no-reveal design).

### `ResetPasswordComponent` â€” `/reset-password`

**File:** `client/src/app/auth/reset-password.component.ts`

- Reads `?token=â€¦` from query params on init.
- Validates the token via `GET /auth/validate-reset-token?token=â€¦`; shows an error message if invalid or expired.
- New password input with real-time strength indicator (Weak / Medium / Strong) and a confirmation field.
- Calls `POST /auth/reset-password` with token + new password; navigates to `/login` on success.

### `ChangeExpiredPasswordComponent` â€” `/change-expired-password`

**File:** `client/src/app/auth/change-expired-password.component.ts`

- Reads `?userId=â€¦` from query params.
- New password form (no current-password required; the userId acts as the credential for this one-time change).
- Calls `POST /auth/change-expired-password`; on success stores the returned JWT (full login) and navigates to `/dashboard`.

### Core Service: `AuthService`

**File:** `client/src/app/core/services/auth.service.ts`

**Responsibilities:**
- Stores JWT and user claims in `localStorage`.
- Exposes `getUserRole()`, `getUserRoleId()`, `getUserId()`, `getEmail()`, `getUserName()` helpers parsed from the JWT payload.
- `isAuthenticated()` — checks for a non-expired token.
- `logout()` — clears `localStorage`, navigates to `/login`.

---

## End-to-End Data Flow

### Standard Login
```
Browser                  Angular                   API                   DB
  |--- email+password --->|                          |                    |
  |                       |--- POST /auth/login ---->|                    |
  |                       |                          |-- query Users ---->|
  |                       |                          |<-- User row -------|
  |                       |                          | verify hash        |
  |                       |                          | check expiry       |
  |                       |                          | build JWT          |
  |                       |<-- AuthResponse ---------|                    |
  |                       | store JWT in localStorage|                    |
  |                       | subscribeToServer()      |                    |
  |<-- navigate /dashboard|                          |                    |
```

### MFA Login
```
Browser                  Angular                   API
  |--- email+password --->|                          |
  |                       |--- POST /auth/login ---->|
  |                       |<-- requiresMfa=true -----|
  |<-- show TOTP input ---|                          |
  |--- 6-digit code ------>|                         |
  |                       |--- POST /auth/verify-mfa->|
  |                       |<-- JWT token ------------|
  |<-- navigate /dashboard|                          |
```

### Password Reset
```
Browser          Angular              API                Email Server
  |-- enter email->|                   |                     |
  |               |-- POST /forgot --> |                     |
  |               |                   |-- send reset email ->|
  |               |<-- 200 OK --------|                     |
  |<-- success msg|                   |                     |
  | (user clicks link in email)        |                     |
  |-- /reset-password?token=X -------->|                     |
  |               |-- GET /validate -->|                     |
  |               |<-- 200 OK --------|                     |
  |<-- form shown-|                   |                     |
  |-- new password->|                 |                     |
  |               |-- POST /reset  -->|                     |
  |               |<-- 200 OK --------|                     |
  |<-- navigate /login                |                     |
```

