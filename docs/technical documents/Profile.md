# User Profile

## Overview

The Profile section gives every authenticated user access to their own account information and self-service settings. It is organised into sub-pages accessible from a dropdown menu in the top navigation bar. The main profile page handles name editing and password changes. Sub-pages provide MFA setup, biometric credential management, linked devices, care recipient relationships, personal health conditions (CareRecipient only), submitted reports, and notification preferences.

---

## Role Access

All authenticated roles can access the Profile section. Specific sub-pages are guarded by role:
- `/profile/my-conditions` — `CareRecipient` only.
- `/profile/care-recipients` — Carer, SupportWorker, CareRecipient, HealthCareProvider only. Admins use the Management module instead.
- All other sub-pages — all roles.

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

### Google Drive: `GoogleDriveAuthController` — `/api/google-drive`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/google-drive/connect` | Authorized | Returns Google OAuth2 authorization URL |
| GET | `/api/google-drive/callback` | Anonymous | OAuth2 callback — exchanges code for tokens |
| DELETE | `/api/google-drive/disconnect` | Authorized | Removes stored Drive tokens for current user |
| GET | `/api/export/drive-status/{careRecipientId}` | Authorized | Returns whether a CR has connected Google Drive |

**Connect flow:**
1. `GET /google-drive/connect` — generates an OAuth2 URL using `GoogleDriveClientId`, `ClientSecret`, and `RedirectUri` from `AppConfigService`. Stores a `state → userId` mapping in `IMemoryCache` (15-minute TTL).
2. Browser is redirected to Google's OAuth2 consent screen.
3. Google redirects back to `GET /google-drive/callback?code=…&state=…`. The API exchanges the code for an access/refresh token pair and upserts a `UserGoogleDriveToken` row.
4. API redirects back to the Angular frontend at `GoogleDriveRedirectUri/profile?drive=connected`.

---

## Frontend

### Routes (ProfileModule)

| Path | Component | Guard |
|---|---|---|
| `/profile` | `ProfileComponent` | `AuthGuard` |
| `/profile/security` | `MfaSetupComponent` | `AuthGuard` |
| `/profile/biometric` | `BiometricSetupComponent` | `AuthGuard` |
| `/profile/care-recipients` | `CareRecipientsComponent` | `AuthGuard` + role |
| `/profile/devices` | `LinkedDevicesComponent` | `AuthGuard` |
| `/profile/my-conditions` | `MyConditionsComponent` | `AuthGuard` + `CareRecipient` only |
| `/profile/my-reports` | `MyReportsComponent` | `AuthGuard` |
| `/profile/notifications` | `NotificationPreferencesComponent` | `AuthGuard` |

---

### `ProfileComponent` — `/profile`

**File:** `client/src/app/features/profile/components/profile.component.ts`

**Displays (read-only):** Email address, role name.

**Editable fields:** First name, last name.

**Password change section:** Conditionally shown via an expand toggle. Requires current password; validates new password strength client-side (min 8, upper, lower, digit) and a confirmation field.

**Save:** Calls `PUT /auth/profile`. Updates `AuthService` localStorage (`firstName`, `lastName`) on success so the nav bar greeting reflects the change immediately.

#### Google Drive Section
- Visible for CareRecipient and Admin roles.
- On init: calls `GoogleDriveService.getDriveStatus()` (`GET /google-drive/status`) to check if the current user has connected Drive.
- **Connect button:** Calls `GET /google-drive/connect`; the returned URL is opened via `window.location.href` to start the OAuth2 flow.
- **Disconnect button:** Calls `GoogleDriveService.disconnect()` (`DELETE /google-drive/disconnect`).
- On return from Google, the Angular router checks for `?drive=connected` or `?drive=error` query params and shows a snackbar accordingly.

---

### `MfaSetupComponent` — `/profile/security`

**File:** `client/src/app/features/profile/components/mfa-setup.component.ts`

**Hub screen:** Shows current MFA status (enabled/disabled) with action buttons.

**Setup wizard (6 steps):**
1. **Hub** — status display with "Set Up MFA" or "Disable MFA" buttons.
2. **Intro** — explains TOTP and the need for an authenticator app.
3. **Scan** — calls `POST /auth/mfa/setup`; displays the returned base64 QR code image and the raw Base32 secret for manual entry.
4. **Verify** — 6-digit TOTP code input; calls `POST /auth/mfa/confirm` to validate and save.
5. **Backup** — displays the 10 one-time backup codes returned by `/mfa/setup`. Offers download (`.txt` file) and clipboard copy.
6. **Complete** — success screen; updates `userInfo.isMfaEnabled` in `localStorage`.

**Disable MFA:** Shows a confirmation dialog, then requires a valid TOTP code. Calls `POST /auth/mfa/disable`. Updates localStorage.

---

### `BiometricSetupComponent` — `/profile/biometric`

*(Detailed in [Biometric-Authentication.md](Biometric-Authentication.md))*

Summary: Lists registered WebAuthn credentials, allows registering new ones (name + OS biometric prompt), and deleting existing ones. Gated on `PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()`.

---

### `CareRecipientsComponent` — `/profile/care-recipients`

**File:** `client/src/app/features/profile/components/care-recipients.component.ts`

**Carer / SupportWorker / HealthCareProvider mode:**
- Lists linked care recipients with name, email, date of birth (age computed), year diagnosed, diabetes type label.
- **Add CR:** "Add Care Recipient" form with an email text field; calls `POST /carerecipient` with `{ email }`. Refreshes list on success.
- **Remove CR:** "Remove" button with confirmation dialog; calls `DELETE /carerecipient/{careRecipientId}`.
- Search filter on name and email.

**CareRecipient mode:**
- Lists linked carers and support workers with name, email, role badge.
- **Submit delink request:** "Request Removal" button per carer row; calls `POST /delink-requests` via `DelinkRequestService`. The button changes to a "Pending" badge for the duration (`pendingDelinkCarerIds` Set tracks in-progress requests). The Admin must approve or reject the request.

---

### `LinkedDevicesComponent` — `/profile/devices`

**File:** `client/src/app/features/profile/components/linked-devices.component.ts`

**On init:** Calls `DeviceService.getDevices()` (`GET /me/devices`).

**Device list:** Shows each registered browser/device with:
- Manufacturer + model (e.g., "Apple / MacBook Pro").
- Device type and OS version.
- Friendly name (editable inline).
- "Current device" badge if `clientId` in `localStorage` matches the device's `ClientId`.
- Last seen date.

**Rename:** Inline edit input per device; saves via `DeviceService.renameDevice(id, name)` (`PATCH /me/devices/{id}/rename`).

**Remove:** `DELETE /me/devices/{id}` (soft-delete — device becomes inactive).

**Reset install prompt:** For the current device, shows a "Reset Install Banner" option that calls `DeviceService.resetInstallPrompt()`, setting the PWA install status back to `Unknown` so the install banner can appear again.

---

### `MyConditionsComponent` — `/profile/my-conditions` (CareRecipient only)

**File:** `client/src/app/features/profile/components/my-conditions.component.ts`

**On init:** Calls `UserConditionsService.getMyConditions()` and `UserConditionsService.getBpMedications()` and `UserConditionsService.getDiabetesMedications()`.

#### Conditions Section
- Shows current conditions with year diagnosed.
- **Add condition:** Dropdown filtered to conditions not yet added; optional year field.
  - `POST /user-conditions`
- **Remove condition:** `DELETE /user-conditions/{id}`.

#### BP Medications Section *(shown if HBP condition is present)*
- Table: Medication Name | Dose | Dose Unit | Frequency | Notes | Active | Actions.
- **Add/Edit form:** `medicationName`, `dose`, `doseUnit`, `frequency`, `notes`, `isActive` toggle.
  - Create: `POST /user-conditions/bp-medications`
  - Update: `PUT /user-conditions/bp-medications/{id}`
- **Deactivate** sets `isActive = false` (soft-remove from active medication lookup).

#### Diabetes Medications Section *(shown if T1D or T2D condition is present)*
- Table: Medication Name | Delivery Route | Insulin Method | Pump Name | Notes | Active | Actions.
- **Add/Edit form:** `medicationName`, `deliveryRoute` (Pills/Injection), `insulinDeliveryMethod` (Pump/Injections), `pumpName`, `notes`, `isActive` toggle.
  - Create: `POST /user-conditions/diabetes-medications`
  - Update: `PUT /user-conditions/diabetes-medications/{id}`

---

### `MyReportsComponent` — `/profile/my-reports`

**File:** `client/src/app/features/profile/components/my-reports.component.ts`

**On init:** Calls `FeatureBugReportService.getMyReports()` (`GET /feature-bug-reports/my-reports`).

**Report list:** Shows title, type (Feature Request / Bug Report), priority badge, status badge (New / In Review / Closed), and submission date.

**Submit new report:** "New Report" button opens `ReportFeatureBugComponent` as a `MatDialog`. On close (if result = `true`), refreshes the list.

---

### `NotificationPreferencesComponent` — `/profile/notifications`

**File:** `client/src/app/features/profile/components/notification-preferences.component.ts`

**On init:** Calls `NotificationPreferenceService.getPreferences()` (`GET /notification-preferences`).

**Preference groups:**
- **Health Alerts** — types marked `IsAdminControlled = true` (e.g., IncidentSeverity, AssessmentSeverity, BgTimerReminder). Displayed as read-only badges; cannot be toggled by the user.
- **Other Notifications** — user-configurable types (e.g., General, CarerRemoved, DelinkApproved, FeatureBugReportResolved, SupplyRunningLow). Each has a toggle switch.

**Display names:** CamelCase type names are converted to display strings by inserting spaces before uppercase letters (e.g., `BgTimerReminder` → "Bg Timer Reminder").

**Save:** Calls `NotificationPreferenceService.updatePreferences(items)` (`PUT /notification-preferences`) with the array of `{ typeId, isEnabled }` objects. Backend ignores admin-controlled types if sent by a non-admin.

---

## Service Interactions

| Service | Used By | Key Calls |
|---|---|---|
| `AuthService` | ProfileComponent | `PUT /auth/profile` |
| `GoogleDriveService` | ProfileComponent | `GET /google-drive/status`, `GET /google-drive/connect`, `DELETE /google-drive/disconnect` |
| `CareRecipientService` | CareRecipientsComponent | `POST/DELETE /carerecipient` |
| `DelinkRequestService` | CareRecipientsComponent | `POST /delink-requests` |
| `DeviceService` | LinkedDevicesComponent | `GET/PATCH/DELETE /me/devices` |
| `UserConditionsService` | MyConditionsComponent | `GET/POST/DELETE /user-conditions`, BP/Diabetes medication CRUD |
| `FeatureBugReportService` | MyReportsComponent | `GET /feature-bug-reports/my-reports` |
| `NotificationPreferenceService` | NotificationPreferencesComponent | `GET/PUT /notification-preferences` |
