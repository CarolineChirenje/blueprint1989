# User Profile

## Overview

The Profile section gives every authenticated user access to their own account information and self-service settings. It is organised into sub-pages accessible from a dropdown menu in the top navigation bar. The main profile page handles name editing and password changes. Sub-pages provide MFA setup, biometric credential management, linked devices, submitted reports, and notification preferences.

---

## Role Access

All authenticated roles can access the Profile section. All sub-pages are available to all roles.

---

## Backend

### Profile Update: `PUT /api/auth/profile`

| Method | Route | Auth | Description |
|---|---|---|---|
| PUT | `/api/auth/profile` | Authorized | Update first/last name and/or password |

**Request DTO: `UpdateProfileRequest`**
```
firstName       string?
lastName        string?
currentPassword string?   Required when newPassword is provided
newPassword     string?   Min 8 chars, upper + lower + digit
```

Logic:
- If `newPassword` is provided, `currentPassword` must match the stored hash.
- Updates `PasswordLastChanged` to `now` on successful password change.
- Returns the updated user's basic fields.

---

## Frontend

### Routes (ProfileModule)

| Path | Component | Guard |
|---|---|---|
| `/profile` | `ProfileComponent` | `AuthGuard` |
| `/profile/security` | `MfaSetupComponent` | `AuthGuard` |
| `/profile/biometric` | `BiometricSetupComponent` | `AuthGuard` |
| `/profile/devices` | `LinkedDevicesComponent` | `AuthGuard` |
| `/profile/my-reports` | `MyReportsComponent` | `AuthGuard` |
| `/profile/notifications` | `NotificationPreferencesComponent` | `AuthGuard` |

---

### `ProfileComponent` â€” `/profile`

**File:** `client/src/app/features/profile/components/profile.component.ts`

**Displays (read-only):** Email address, role name.

**Editable fields:** First name, last name.

**Password change section:** Conditionally shown via an expand toggle. Requires current password; validates new password strength client-side (min 8, upper, lower, digit) and a confirmation field.

**Save:** Calls `PUT /auth/profile`. Updates `AuthService` localStorage (`firstName`, `lastName`) on success so the nav bar greeting reflects the change immediately.

---

### `MfaSetupComponent` â€” `/profile/security`

**File:** `client/src/app/features/profile/components/mfa-setup.component.ts`

**Hub screen:** Shows current MFA status (enabled/disabled) with action buttons.

**Setup wizard (6 steps):**
1. **Hub** â€” status display with "Set Up MFA" or "Disable MFA" buttons.
2. **Intro** â€” explains TOTP and the need for an authenticator app.
3. **Scan** â€” calls `POST /auth/mfa/setup`; displays the returned base64 QR code image and the raw Base32 secret for manual entry.
4. **Verify** â€” 6-digit TOTP code input; calls `POST /auth/mfa/confirm` to validate and save.
5. **Backup** â€” displays the 10 one-time backup codes returned by `/mfa/setup`. Offers download (`.txt` file) and clipboard copy.
6. **Complete** â€” success screen; updates `userInfo.isMfaEnabled` in `localStorage`.

**Disable MFA:** Shows a confirmation dialog, then requires a valid TOTP code. Calls `POST /auth/mfa/disable`. Updates localStorage.

---

### `BiometricSetupComponent` â€” `/profile/biometric`

*(Detailed in [Biometric-Authentication.md](Biometric-Authentication.md))*

Summary: Lists registered WebAuthn credentials, allows registering new ones (name + OS biometric prompt), and deleting existing ones. Gated on `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()`.

---

### `LinkedDevicesComponent` â€” `/profile/devices`

**File:** `client/src/app/features/profile/components/linked-devices.component.ts`

**On init:** Calls `DeviceService.getDevices()` (`GET /me/devices`).

**Device list:** Shows each registered browser/device with:
- Manufacturer + model (e.g., "Apple / MacBook Pro").
- Device type and OS version.
- Friendly name (editable inline).
- "Current device" badge if `clientId` in `localStorage` matches the device's `ClientId`.
- Last seen date.

**Rename:** Inline edit input per device; saves via `DeviceService.renameDevice(id, name)` (`PATCH /me/devices/{id}/rename`).

**Remove:** `DELETE /me/devices/{id}` (soft-delete â€” device becomes inactive).

**Reset install prompt:** For the current device, shows a "Reset Install Banner" option that calls `DeviceService.resetInstallPrompt()`, setting the PWA install status back to `Unknown` so the install banner can appear again.

---

### `MyReportsComponent` â€” `/profile/my-reports`

**File:** `client/src/app/features/profile/components/my-reports.component.ts`

**On init:** Calls `FeatureBugReportService.getMyReports()` (`GET /feature-bug-reports/my-reports`).

**Report list:** Shows title, type (Feature Request / Bug Report), priority badge, status badge (New / In Review / Closed), and submission date.

**Submit new report:** "New Report" button opens `ReportFeatureBugComponent` as a `MatDialog`. On close (if result = `true`), refreshes the list.

---

### `NotificationPreferencesComponent` â€” `/profile/notifications`

**File:** `client/src/app/features/profile/components/notification-preferences.component.ts`

**On init:** Calls `NotificationPreferenceService.getPreferences()` (`GET /notification-preferences`).

**Preference list:** All 5 notification types shown as toggle switches.

**Save:** Calls `NotificationPreferenceService.updatePreferences(items)` (`PUT /notification-preferences`) with the array of `{ typeId, isEnabled }` objects.

---

## Service Interactions

| Service | Used By | Key Calls |
|---|---|---|
| `AuthService` | ProfileComponent | `PUT /auth/profile` |
| `DeviceService` | LinkedDevicesComponent | `GET/PATCH/DELETE /me/devices` |
| `FeatureBugReportService` | MyReportsComponent | `GET /feature-bug-reports/my-reports` |
| `NotificationPreferenceService` | NotificationPreferencesComponent | `GET/PUT /notification-preferences` |
