# Notifications

## Overview

Vitara uses a dual-channel notification system: an in-app notification inbox (the bell icon in the navigation bar) and browser Web Push notifications. Every notification is always persisted to the in-app inbox first; a push is sent additionally if the user has a registered subscription and has not opted out of that notification type. Users can configure which types of push notifications they receive from Profile → Notifications. Administrators can override a user's preferences for health-alert types that are designated as admin-controlled.

---

## Role Access

| Feature | All Roles | Admin/SuperAdmin Only |
|---|---|---|
| View in-app notifications | ✓ | — |
| Mark notifications read | ✓ | — |
| Configure own non-health preferences | ✓ | — |
| Configure health-alert preferences | — | ✓ (via admin panel) |
| Manage any user's preferences | — | ✓ |
| Send test push | ✓ (self, any type) | Required in production |

---

## Notification Types

| ID | Name | Admin-Controlled |
|---|---|---|
| 1 | General | No |
| 2 | IncidentSeverity | Yes |
| 3 | AssessmentSeverity | Yes |
| 4 | CarerRemoved | No |
| 5 | CareRecipientRemoved | No |
| 6 | BgTimerReminder | Yes |
| 7 | DelinkApproved | No |
| 8 | DelinkDenied | No |
| 9 | BloodPressureAlert | Yes |
| 10 | FeatureBugReportResolved | No |
| 11 | SupplyRunningLow | No |
| 12 | BpIncidentSeverity | Yes |

Admin-controlled types (IDs 2, 3, 6, 9, 12) can only be toggled by Administrators and SuperAdmins, not by the users themselves.

---

## Backend

### Controller: `NotificationController` — `/api/notification`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/notification` | Authorized | Get all notifications + unread count for current user |
| PUT | `/api/notification/{id}/read` | Authorized | Mark one notification as read |
| PUT | `/api/notification/read-all` | Authorized | Mark all notifications as read |
| POST | `/api/notification/ketone-alert` | SupportOrHigher | Manually trigger ketone alert for a care recipient |

### Controller: `NotificationPreferenceController`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/notification-preferences` | Authorized | Get own preferences (all 12 types) |
| PUT | `/api/notification-preferences` | Authorized | Update own non-admin-controlled preferences |
| GET | `/api/admin/notification-preferences/{userId}` | AdminOrHigher | Get any user's preferences |
| PUT | `/api/admin/notification-preferences/{userId}` | AdminOrHigher | Update any type for any user |

### Controller: `PushController` — `/api/push`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/push/vapid-public-key` | Anonymous | Returns VAPID public key |
| POST | `/api/push/subscribe` | Authorized | Register or refresh a browser push subscription |
| DELETE | `/api/push/unsubscribe` | Authorized | Remove a push subscription by endpoint |
| POST | `/api/push/test` | Authorized (Admin in prod) | Send a test push notification to self |
| POST | `/api/push/bg-timer/schedule` | AllRoles | Schedule a BGL recheck timer reminder |
| DELETE | `/api/push/bg-timer/cancel` | AllRoles | Cancel a pending BG timer reminder |

#### `POST /api/push/subscribe` — Upsert Subscription
```
endpoint     string    Web Push endpoint URL
p256dh       string    Public key (base64)
auth         string    Auth secret (base64)
userAgent    string?   Browser user agent
```
Finds existing `PushSubscription` by `endpoint` or creates a new one. Updates the `Auth` and `P256dh` keys if they changed.

#### `POST /api/push/bg-timer/schedule`
```
careRecipientId   int    Target care recipient
delayMinutes      int?   Default 120 min, max 480 min
```
Cancels any existing pending `BgTimerReminder` for the same `(userId, careRecipientId)` pair, then inserts a new one with `ScheduledAt = now + delayMinutes`.

### Service: `NotificationService`

**File:** `server/src/Vitara.Api/Services/NotificationService.cs`

**Dependencies:** `ApplicationDbContext`

Operations:
- `CreateAsync(userId, message, type, deepLinkUrl?, relatedEntityId?)` — persists a `Notification` row.
- `GetForUserAsync(userId)` — returns `NotificationSummaryDto` with a list of notifications + total unread count.
- `MarkReadAsync(id, userId)` — sets `IsRead = true` for a single notification (validates ownership).
- `MarkAllReadAsync(userId)` — bulk update via `ExecuteUpdateAsync`.

### Service: `PushNotificationSender`

**File:** `server/src/Vitara.Api/Services/PushNotificationSender.cs`

**Dependencies:** `ApplicationDbContext`, `NotificationService`, VAPID settings, `HttpClient`

**`SendToUserAsync(userId, payload)`** — full pipeline:

1. **Always creates in-app notification** via `NotificationService.CreateAsync()` first.
2. **Preference check:** Queries `UserNotificationPreferences` for this user + notification type. If `IsEnabled = false`, skips push (in-app record still created).
3. **Load subscriptions:** Queries all `PushSubscription` rows for the user.
4. **Dispatch:** Sends VAPID-signed HTTP POST to each subscription endpoint using `Lib.Net.Http.WebPush`. All dispatches run in parallel (`Task.WhenAll`).
5. **Pruning:** Any subscription that returns HTTP 410 (Gone) or 404 is automatically deleted from the database.

**`SendToUsersAsync(userIds[], payload)`** — calls `SendToUserAsync` for each ID.

### Service: `NotificationPreferenceService`

**File:** `server/src/Vitara.Api/Services/NotificationPreferenceService.cs`

**`GetPreferencesAsync(userId)`**
- Loads all 12 `NotificationTypeEntity` rows.
- Joins with `UserNotificationPreference` rows for the user.
- For types with no stored preference row, defaults to `IsEnabled = true`.
- Returns a complete list of 12 `NotificationPreferenceDto` items.

**`UpdatePreferencesAsync(userId, items, isAdmin)`**
- For each item in the request:
  - If `isAdmin = false` and the type's `IsAdminControlled = true`, skips the item silently.
  - Otherwise, upserts the `UserNotificationPreference` row for `(userId, typeId)`.

### Model: `Notification`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User |
| `Message` | string | Notification text |
| `IsRead` | bool | |
| `CreatedAt` | DateTime | |
| `Type` | NotificationType | Enum 1–12 |
| `DeepLinkUrl` | string? | Angular route to navigate on click |
| `RelatedEntityId` | int? | ID of the related entity (incident, etc.) |
| `SentViaPush` | bool | Whether a push was dispatched |

### Model: `PushSubscription`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User |
| `Endpoint` | string | Web Push endpoint URL |
| `P256dh` | string | Client public key |
| `Auth` | string | Auth secret |
| `UserAgent` | string? | |
| `CreatedAt` | DateTime | |

### Model: `UserNotificationPreference`

| Field | Type | Notes |
|---|---|---|
| `UserId` | int | PK (composite) |
| `NotificationTypeId` | int | PK (composite) FK → NotificationTypeEntity |
| `IsEnabled` | bool | |

---

## Frontend

### In-App Notification Bell

**File:** `client/src/app/app.component.ts`

- Bell icon in the top navigation bar with a badge showing the unread count.
- On icon click, calls `NotificationService.getNotifications()` (`GET /notification`) to load the full inbox.
- Dropdown panel lists notifications ordered by `createdAt` descending.
- Clicking a notification: marks it read (`PUT /notification/{id}/read`), then navigates to `DeepLinkUrl` if present.
- "Mark All Read" button: `PUT /notification/read-all`.

### Push Subscription Management

**File:** `client/src/app/core/services/push-notification.service.ts`

**`subscribeToServer()`** — called on every login:
1. Checks `Notification.permission`; if not granted, requests permission.
2. Fetches VAPID public key from `GET /push/vapid-public-key`.
3. Calls `navigator.serviceWorker.ready` → `registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: vapidKey })`.
4. Posts subscription to `POST /push/subscribe` with endpoint, p256dh, auth.

**`unsubscribeFromServer()`**
1. Gets current subscription from `pushManager.getSubscription()`.
2. Calls `subscription.unsubscribe()` (browser side).
3. Calls `DELETE /push/unsubscribe` with the endpoint.

**`sendTestPush(payload)`** → `POST /push/test`.

### Notification Preferences Component

**File:** `client/src/app/features/profile/components/notification-preferences/notification-preferences.component.ts`
**Route:** `/profile/notifications`

- Loads `GET /notification-preferences` on init.
- **Health Alerts section:** Types with `isAdminControlled = true` shown as read-only labels with their current state (enabled/disabled).
- **Timer Alarm Sound section:** Device-local toggle that mutes/unmutes the in-browser audio alarm played when a health timer (BGL or blood pressure) expires. Stored in `localStorage` under key `vitara-alarm-muted`. Not synced to the server — preference is per-device.
- **My Preferences section:** Types with `isAdminControlled = false` shown as toggle switches.
- **Save button:** Calls `PUT /notification-preferences` with the array of changed preferences (timer alarm mute is saved immediately, not via this button).

### Admin: Per-User Notification Preferences

**File:** `client/src/app/features/management/components/user-management.component.ts`

- Bell icon button per user row in the User Management table.
- Opens a modal panel loaded with `GET /admin/notification-preferences/{userId}`.
- Displays all 12 notification types with individual toggle switches.
- Save calls `PUT /admin/notification-preferences/{userId}`.

---

## Notification Seeding on Registration

When a new user registers (`AuthService.SignupAsync`), all 12 `UserNotificationPreference` rows are inserted with `IsEnabled = true` to ensure the user receives all notifications by default until they explicitly opt out.

---

## Timer Alarm System (In-Browser)

Health timers in Vitara play an audio alarm, vibrate the device, and show a dismissible overlay when they expire. This is separate from push notifications and works within the open browser tab.

### Covered Timers

| Timer | Location | Duration | Notes |
|---|---|---|---|
| BGL Hypo recheck | `bgl-reading.component` | 10 or 15 min | 10 min for check-only cycles (BGL 3.9 after cycle 2) |
| BGL Ketone monitoring | `bgl-reading.component` | 2 hours | Saves in-progress state to server so any device can resume |
| BP rest between readings | `bp-entry.component` | 2 min | No server persistence — too short to need it |

### Features

| Feature | Detail |
|---|---|
| Audio alarm | 3 × 880 Hz sine-wave beeps via Web Audio API. iOS Safari requires the `AudioContext` to be unlocked during a user gesture — handled automatically on "Start Timer" tap. |
| Vibration | `navigator.vibrate([500, 100, 500, 100, 500])` — silently ignored on iOS and desktop. |
| Screen wake lock | `navigator.wakeLock.request('screen')` acquired when a timer starts; released on completion/skip/cancel. Re-acquired after browser tab returns to foreground (OS releases the sentinel automatically on background). |
| Urgency colours | Timer turns amber at ≤ 60 s remaining, red at ≤ 10 s. Each timer shape has its own CSS: BGL hypo uses a gradient `div` circle; BGL ketone uses plain text; BP uses an SVG ring (stroke colour). |
| Pulse animation | `@keyframes timerPulse` (scale 1→1.05) applied at critical threshold. Named `timerPulse` to avoid collision with the existing `pulse` keyframes used by `.emergency-icon`. |
| Alarm overlay | Dismissible bottom-sheet on mobile (≤ 600 px), centred card modal on desktop (> 600 px). Backdrop click or Escape key dismisses. |
| Screen reader | `aria-live="assertive"` span announces "Less than 1 minute remaining" and "Less than 10 seconds remaining" at urgency transitions. |
| Touch targets | Skip-timer buttons get `min-height: 48px` via `.btn-skip-timer` class. |
| Mute preference | Stored in `localStorage` key `vitara-alarm-muted`. Toggle available in **Profile → Notification Preferences** under "Timer Alarm Sound". |

### Services

**`AudioAlarmService`** — `client/src/app/core/services/audio-alarm.service.ts`

- `unlock(ctx: AudioContext)` — call on user gesture to unblock iOS Safari. Plays a silent 1-sample buffer.
- `playAlarm()` — resumes the stored context, schedules 3 beeps, then returns. No-ops if muted or context is unavailable.
- `isMuted() / setMuted(val)` — reads/writes `localStorage` key `vitara-alarm-muted`.

**`WakeLockService`** — `client/src/app/core/services/wake-lock.service.ts`

- `acquire()` — requests a screen wake lock; silently no-ops if the Wake Lock API is unsupported.
- `release()` — releases the active sentinel.
- `reacquire()` — called from `visibilitychange` handlers to re-request the lock after the tab returns to the foreground (OS revokes the sentinel on background).

### BGL Timer Persistence (Hypo + Ketone)

Both BGL timers now persist their scheduled expiry time and restore it on page reload.

| | Ketone 2-hr timer | Hypo 10/15-min timer |
|---|---|---|
| localStorage key | `bgl-ketone-timer` | `bgl-hypo-timer` |
| Server persistence | Yes — saved as `outcome: 5 MonitoringInProgress` | No |
| VAPID push scheduled | Yes — `POST /push/bg-timer/schedule` (120 min) | Yes — `POST /push/bg-timer/schedule` (10 or 15 min) |
| Wall-clock accuracy | Yes — `timerScheduledAtMs` anchor | Yes — same field, now set in `startTimer()` |

The `ServiceWorkerNotificationService` (`sw-notification.service.ts`) was extended with:
- `readonly HYPO_KEY = 'bgl-hypo-timer'` and `readonly KETONE_KEY = 'bgl-ketone-timer'` public fields.
- An optional `key?: string` parameter added to `scheduleNotification`, `saveTimerState`, `getTimerState`, `clearTimerState`, `hasActiveTimer`, and `getRemainingTime`. Default is `KETONE_KEY` — all existing callers are unaffected.

### BP Timer

The BP rest timer has the same alarm/vibration/wake lock experience but has no server persistence and no VAPID push (2 minutes is too short to warrant a background push).

---

## Service Worker Push Handling

When the browser receives a push event, the service worker (`client/src/custom-sw.js`) processes it:
1. Parses the push data payload (JSON: `{ title, body, deepLinkUrl?, type }`).
2. Calls `self.registration.showNotification(title, { body, data: { deepLinkUrl } })`.
3. On `notificationclick` event: focuses an existing Vitara window if open, or opens a new one, navigating to `deepLinkUrl` if provided.

---

## End-to-End Push Flow

```
API                         PushNotificationSender           Browser
  |                         |                                  |
  | Incident severity High  |                                  |
  |-- SendToUserAsync(carerId, payload)                        |
  |                         |-- create in-app notification     |
  |                         |-- check preference: enabled?     |
  |                         |-- load subscriptions             |
  |                         |-- VAPID-signed POST to endpoint->|
  |                         |                                  |-- show notification
  |                         |                                  | (even if tab closed)
  |                         | 410? → delete subscription       |
  |                         |                                  |
  | Carer clicks notification|                                 |
  |                         |<-- notificationclick event ------|
  |                         |-- navigate to deepLinkUrl        |
  |                         |-- PUT /notification/{id}/read -->|
```
