# App Configuration

## Overview

App Configuration provides a runtime key-value store for application settings that need to be adjustable without redeployment. Settings are persisted in the `AppConfig` database table and loaded on demand by `AppConfigService`. The system supports string, integer, boolean, and secret value types, grouped into logical categories. Administrators can edit all non-read-only keys through the Management console. Changes take effect immediately for most settings; a small number of keys require the API server to be restarted.

---

## Role Access

| Action | Roles |
|---|---|
| Read app config | SuperAdmin, Administrator |
| Update app config | SuperAdmin, Administrator |
| Bulk update | SuperAdmin, Administrator |

---

## Backend

### Controller â€” `AppConfigController`

**File:** `server/src/Blueprint1989.Api/Controllers/AppConfigController.cs`

Base route: `/api/app-config`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/` | `AdminOrHigher` | Returns all config entries |
| GET | `/{key}` | `AdminOrHigher` | Returns a single entry by key |
| PUT | `/{key}` | `AdminOrHigher` | Updates a single config value |
| PUT | `/bulk` | `AdminOrHigher` | Updates multiple config values in one call |
| POST | `/reset/{key}` | `SuperAdminOnly` | Resets a key to its seed/default value |

#### `PUT /bulk`

Accepts an array of key-value pairs:

```json
[
  { "key": "SUPPLY_REPORT_DAY_OF_WEEK", "value": "2" },
  { "key": "BG_TIMER_MAX_MINUTES", "value": "60" }
]
```

Processes each in a single transaction. Returns the count of updated keys.

---

### Model â€” `AppConfigEntry`

**File:** `server/src/Blueprint1989.Api/Models/AppConfigEntry.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Key` | `string` | Unique config key (e.g., `VAPID_PUBLIC_KEY`) |
| `Value` | `string` | Current string value |
| `DefaultValue` | `string?` | Original seed value (used for reset) |
| `Category` | `string` | Grouping label (e.g., `"Push Notifications"`) |
| `Description` | `string` | Human-readable explanation |
| `IsSecret` | `bool` | If true: value masked in UI and API response |
| `IsReadOnly` | `bool` | If true: cannot be updated via API |
| `RequiresRestart` | `bool` | If true: server needs restart for change to take effect |
| `DataType` | `string` | `"string"`, `"int"`, `"bool"` â€” for UI input type |
| `UpdatedAt` | `DateTime` | Last modification timestamp |

---

### Service â€” `AppConfigService`

**File:** `server/src/Blueprint1989.Api/Services/AppConfigService.cs`

#### Key Methods

| Method | Return | Description |
|---|---|---|
| `GetStringAsync(key)` | `string?` | Returns raw value for key |
| `GetIntAsync(key)` | `int?` | Parses value as integer |
| `GetBoolAsync(key)` | `bool?` | Parses value as boolean (`"true"`/`"false"`, `"1"`/`"0"`) |
| `SetAsync(key, value)` | `void` | Updates single key (validates not IsReadOnly) |
| `BulkSetAsync(pairs)` | `int` | Updates multiple keys, returns success count |
| `ResetAsync(key)` | `void` | Copies `DefaultValue` â†’ `Value` |

#### Runtime Reads (No Caching)

`AppConfigService` queries the database on every read (`GetStringAsync` etc.). There is no in-memory cache. This ensures that changes made through the Management console take effect for new requests immediately without any cache invalidation step. The trade-off is an extra DB query per config read; this is acceptable given that config reads occur at well-defined points (typically once per request lifecycle).

#### Read-Only Protection

```csharp
if (entry.IsReadOnly)
    throw new InvalidOperationException($"Key '{key}' is read-only and cannot be modified.");
```

---

## Known AppConfig Keys

### Push Notifications

| Key | Type | Description | Requires Restart |
|---|---|---|---|
| `VAPID_PUBLIC_KEY` | string (secret) | VAPID public key for Web Push | Yes |
| `VAPID_PRIVATE_KEY` | string (secret) | VAPID private key | Yes |
| `VAPID_SUBJECT` | string | VAPID subject (`mailto:` or `https:`) | Yes |

### Authentication

| Key | Type | Description | Requires Restart |
|---|---|---|---|
| `JWT_SECRET` | string (secret) | JWT signing key | Yes |
| `JWT_EXPIRY_MINUTES` | int | Token lifetime in minutes | No |
| `JWT_ISSUER` | string | JWT issuer value | Yes |
| `JWT_AUDIENCE` | string | JWT audience value | Yes |
| `PASSWORD_EXPIRY_DAYS` | int | Days before password is considered expired | No |
| `MFA_CODE_EXPIRY_MINUTES` | int | TOTP window tolerance in minutes | No |

### Email

| Key | Type | Description |
|---|---|---|
| `SMTP_HOST` | string | SMTP server hostname |
| `SMTP_PORT` | int | SMTP port (typically 587) |
| `SMTP_USERNAME` | string (secret) | SMTP login username |
| `SMTP_PASSWORD` | string (secret) | SMTP login password |
| `SMTP_FROM_ADDRESS` | string | `From` email address |
| `SMTP_FROM_NAME` | string | `From` display name |

### General

| Key | Type | Description |
|---|---|---|
| `APP_BASE_URL` | string | Public base URL of the Angular app (used in email links) |
| `API_BASE_URL` | string | Internal API URL |

---

## Frontend

### `AppConfigManagementComponent`

**File:** `client/src/app/features/management/components/app-config-management.component.ts`

Route: `/management/app-config`

#### On Init

Calls `GET /api/app-config`. Groups the returned entries by `category` and displays each group as an `MatExpansionPanel`.

#### Entry Row Display

Each config entry renders as an inline form row:
- **Key** â€” read-only label with monospace styling.
- **Description** â€” subtitle text.
- **Value input** â€” rendered based on `dataType`:
  - `"string"` â†’ `MatInput` text.
  - `"int"` â†’ `MatInput` with `type="number"`.
  - `"bool"` â†’ `MatSlideToggle` (value `"true"`/`"false"`).
- **Secret masking** â€” if `isSecret = true`, the input is `type="password"` by default with a visibility-toggle button.
- **Read-only** â€” if `isReadOnly = true`, the input is disabled and a lock icon is shown.
- **Requires restart badge** â€” if `requiresRestart = true`, a yellow warning chip shown on the row.

#### Dirty Tracking

Each form entry is tracked independently. When a value is changed, the row highlights and a "Save" icon button appears per row (individual saves) and the "Save All Changes" button in the header becomes active.

#### Bulk Save

"Save All Changes" button collects all dirty rows and calls `PUT /app-config/bulk` in one request. On success, dirty flags are cleared and a restart warning banner is shown if any of the saved keys had `requiresRestart = true`.

#### Restart Warning Banner

After saving a key with `requiresRestart = true`, a prominent yellow banner is shown:

> "Some changes require a server restart to take effect. Please restart the API."

Banner persists until the user dismisses it manually.

#### AppConfigService (Angular)

**File:** `client/src/app/features/management/services/app-config.service.ts`

| Method | HTTP | Endpoint |
|---|---|---|
| `getAll()` | GET | `/api/app-config` |
| `getByKey(key)` | GET | `/api/app-config/{key}` |
| `update(key, value)` | PUT | `/api/app-config/{key}` |
| `bulkUpdate(pairs)` | PUT | `/api/app-config/bulk` |
| `reset(key)` | POST | `/api/app-config/reset/{key}` |

---

## Database Seeding

`DatabaseSeeder.SeedAsync` is called on every startup. It seeds all known AppConfig keys with their default values **only if the key does not already exist**:

```csharp
if (!await _db.AppConfig.AnyAsync(c => c.Key == key))
{
    _db.AppConfig.Add(new AppConfigEntry { Key = key, Value = defaultValue, ... });
}
```

This means runtime changes are safe across restarts â€” existing rows are never overwritten by the seeder. Only keys that are missing (e.g., after adding a new config key in code) are inserted.

---

## Security Notes

- **Secret masking on API response:** If `IsSecret = true`, the `GET /app-config` and `GET /app-config/{key}` endpoints return the value as `"â€¢â€¢â€¢â€¢â€¢â€¢â€¢â€¢"` (masked placeholder) rather than the real value. The UI never receives the actual secret.
- **Write-only secrets:** To update a secret (e.g., rotating VAPID keys), the admin types the new value and saves. The backend accepts the new value and persists it; the returned response again masks it.
- **Read-only keys:** Keys marked `IsReadOnly = true` are environment-injected values that should not be changed at runtime. The API rejects any PUT request targeting them.
- **Admin-only access:** The entire `/api/app-config` route group requires the `AdminOrHigher` policy. Regular users cannot read or write any config values.

