# Google Drive Integration

## Overview

Vitara uses **Google Drive API v3** with **OAuth 2.0 Authorization Code flow** (offline access) to allow Care Recipients to upload clinical Excel reports directly to a `Vitara` folder in their personal Google Drive. Credentials are persisted server-side, so uploads can happen without user interaction after the initial one-time consent.

---

## How It Works — End-to-End Flow

```
User (Profile page)
  │
  ├─ 1. Click "Connect Google Drive"
  │       │
  │       └─► GET /api/google-drive/connect
  │               └─ API builds OAuth2 consent URL
  │               └─ Stores state → userId in IMemoryCache (15-min TTL)
  │               └─ Returns { url: "https://accounts.google.com/o/oauth2/auth?..." }
  │
  ├─ 2. Browser redirects to Google consent screen
  │       └─ User grants Drive access
  │
  └─ 3. Google redirects back to
           GET /api/google-drive/callback?code=...&state=...
               └─ API validates state from cache
               └─ Exchanges code for access + refresh tokens
               └─ Upserts UserGoogleDriveToken row in database
               └─ Redirects browser to /profile?drive=connected
```

After connection, any export action from the Incidents or Export screens can push an Excel file to Drive via `POST /api/export/upload-to-drive`.

---

## Prerequisites — Google Cloud Console Setup

Before the integration works, an administrator must register a Google Cloud project and populate three `AppConfig` keys.

### Step 1 — Create a Google Cloud Project

1. Go to [console.cloud.google.com](https://console.cloud.google.com).
2. Click **Select a project → New Project**.
3. Give it a name (e.g. `Vitara`) and create it.

### Step 2 — Enable the Google Drive API

1. In the project, navigate to **APIs & Services → Library**.
2. Search for **Google Drive API** and click **Enable**.

### Step 3 — Configure the OAuth Consent Screen

1. Navigate to **APIs & Services → OAuth consent screen**.
2. Choose **External** (or Internal if using a Google Workspace org).
3. Fill in the required app name, support email, and developer contact.
4. Under **Scopes**, add `https://www.googleapis.com/auth/drive` (or `drive.file` for narrower access).
5. Add test users if the app is still in "Testing" mode.
6. Publish the app once ready for production.

### Step 4 — Create OAuth 2.0 Credentials

1. Navigate to **APIs & Services → Credentials → Create Credentials → OAuth client ID**.
2. Application type: **Web application**.
3. Under **Authorised redirect URIs**, add:
   ```
   https://vitarapi.elroitec.com/api/google-drive/callback
   ```
   > This must exactly match the `GoogleDriveRedirectUri` value in App Config.
4. Click **Create**. Copy the **Client ID** and **Client Secret**.

### Step 5 — Configure App Config

Log in as SuperAdmin or Administrator and set these three keys under **Management → App Configuration**:

| Key | Description | Example |
|---|---|---|
| `GoogleDriveClientId` | OAuth 2.0 Client ID from Google Cloud | `123456789-abc.apps.googleusercontent.com` |
| `GoogleDriveClientSecret` | OAuth 2.0 Client Secret | `GOCSPX-...` |
| `GoogleDriveRedirectUri` | Redirect URI registered in Google Cloud | `https://vitarapi.elroitec.com/api/google-drive/callback` |

> **Security:** `ClientId` and `ClientSecret` are stored as `IsSecret = true` — their values are masked in the UI and never returned in API responses.

---

## Backend

### Controller — `GoogleDriveAuthController`

**File:** `server/src/Vitara.Api/Controllers/GoogleDriveAuthController.cs`

Base route: `/api/google-drive`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/connect` | `Authorized` | Generates and returns the Google OAuth2 consent URL |
| GET | `/callback` | Anonymous | Exchanges auth code for tokens; redirects to frontend |
| GET | `/status` | `Authorized` | Returns `{ connected: bool }` for the calling user |
| DELETE | `/disconnect` | `Authorized` | Revokes and deletes stored Drive tokens for the calling user |

#### `GET /connect`

1. Reads `GoogleDriveClientId`, `GoogleDriveClientSecret`, and `GoogleDriveRedirectUri` from `AppConfigService`.
2. Creates a `GoogleAuthorizationCodeFlow` with scope `https://www.googleapis.com/auth/drive`.
3. Generates a CSRF-protection `state` UUID and caches it against the current `userId` in `IMemoryCache` for **15 minutes**.
4. Builds and returns the authorization URL.

#### `GET /callback`

1. Validates the `state` parameter against the cache.
2. If `error` is present (user denied), redirects to `/profile?drive=error`.
3. Exchanges the `code` for token response using `GoogleAuthorizationCodeFlow.ExchangeCodeForTokenAsync`.
4. Upserts a `UserGoogleDriveToken` row:
   - `UserId` — from the cached state mapping
   - `AccessToken` — short-lived access token
   - `RefreshToken` — long-lived refresh token
   - `TokenExpiry` — calculated from `expires_in`
5. Redirects to `{GoogleDriveFrontendUrl}/profile?drive=connected`.

#### Token Refresh

The Google client library (`Google.Apis.Auth`) automatically refreshes the access token using the stored `RefreshToken` when it expires. `ExportService` reads the persisted token, passes it to `GoogleCredential`, and the library handles refresh transparently. The updated token is then saved back to the database.

---

### Database — `UserGoogleDriveTokens` Table

**Migration:** `20260306013021_AddUserGoogleDriveToken`

| Column | Type | Notes |
|---|---|---|
| `id` | `integer` | PK, auto-increment |
| `user_id` | `integer` | FK → `Users.id` |
| `access_token` | `varchar(2048)` | Current OAuth access token |
| `refresh_token` | `varchar(512)` | Long-lived refresh token |
| `token_expiry` | `timestamp` | UTC expiry of the access token |
| `created_at` | `timestamp` | Row insertion time |
| `updated_at` | `timestamp` | Last upsert time |

One row per user. Upserted on each successful OAuth callback so re-authorisation always refreshes all fields.

---

### App Config Keys

**File:** `server/src/Vitara.Api/Models/AppConfigKeys.cs`

| Constant | Key String | Category |
|---|---|---|
| `AppConfigKeys.GoogleDriveClientId` | `GoogleDriveClientId` | `GoogleDrive` |
| `AppConfigKeys.GoogleDriveClientSecret` | `GoogleDriveClientSecret` | `GoogleDrive` |
| `AppConfigKeys.GoogleDriveRedirectUri` | `GoogleDriveRedirectUri` | `GoogleDrive` |
| `AppConfigKeys.GoogleDriveFrontendUrl` | `GoogleDriveFrontendUrl` | `GoogleDrive` |

---

### Service — `ExportService` (Drive Upload)

**File:** `server/src/Vitara.Api/Services/ExportService.cs`

#### `UploadToGoogleDriveAsync(byte[] excelData, int careRecipientId, string fileName)`

1. Loads `UserGoogleDriveToken` for the `careRecipientId`. Throws if not found.
2. Reads `GoogleDriveClientId` and `GoogleDriveClientSecret` from `AppConfigService`.
3. Constructs a `GoogleCredential` from the stored tokens.
4. Initialises `Google.Apis.Drive.v3.DriveService`.
5. Searches for an existing folder named `"Vitara"` in the user's Drive.
   - If not found, creates a new folder with `mimeType = "application/vnd.google-apps.folder"`.
6. Checks for an existing file with the same `fileName` in the `Vitara` folder.
   - If found, **updates** (replaces) the file content.
   - If not found, **creates** a new file.
7. Returns the file name on success.

#### `GetCareRecipientDriveStatusAsync(int careRecipientId)`

Returns `true` if a `UserGoogleDriveToken` row exists for the given Care Recipient.

---

## Frontend

### Service — `GoogleDriveService`

**File:** `client/src/app/core/services/google-drive.service.ts`

| Method | HTTP | Endpoint | Description |
|---|---|---|---|
| `getConnectUrl()` | GET | `/api/google-drive/connect` | Returns OAuth consent URL |
| `getOwnStatus()` | GET | `/api/google-drive/status` | Returns connection status for calling user |
| `getCareRecipientDriveStatus(id)` | GET | `/api/export/drive-status/{id}` | Returns connection status for a CR |
| `disconnect()` | DELETE | `/api/google-drive/disconnect` | Removes Drive tokens |

All calls include the JWT Bearer token in the `Authorization` header.

---

### Profile Page Integration

**File:** `client/src/app/features/profile/components/profile.component.ts`

The Google Drive section is visible only when the logged-in user has the `CareRecipient` role.

| Action | Method | Behaviour |
|---|---|---|
| Page load | `getOwnStatus()` | Sets `driveConnected` flag; shows "Connected" badge or "Not connected" |
| Connect button | `getConnectUrl()` | Redirects `window.location.href` to Google consent screen |
| Disconnect button | `disconnect()` | Removes tokens; sets `driveConnected = false` |
| Return from Google | Router query params | Reads `?drive=connected` or `?drive=error`; shows snackbar message |

---

### Incidents / Export Page Integration

The **Incidents** and **Export** pages check `getCareRecipientDriveStatus(careRecipientId)` after a Care Recipient is selected. If connected, a **"Upload to Drive"** button is shown alongside the download button. Clicking it calls `POST /api/export/upload-to-drive`.

---

## Security Notes

- OAuth `state` parameter is generated as a random UUID and validated on callback to prevent CSRF attacks.
- `ClientId` and `ClientSecret` are never returned in API responses (`IsSecret = true`).
- Tokens are stored per-user; no token is shared between users.
- Disconnect (`DELETE /google-drive/disconnect`) deletes the stored tokens immediately, preventing further uploads.
- The Drive scope (`https://www.googleapis.com/auth/drive`) grants access to the user's full Drive. For tighter isolation, consider `drive.file` scope (access only to files created by the app) when re-registering the consent screen.
