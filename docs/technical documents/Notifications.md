# Notifications

## Overview

Batanai uses a dual-channel notification system: an in-app notification inbox (the bell icon in the navigation bar) and browser Web Push notifications. Every notification is always persisted to the in-app inbox first; a push is sent additionally if the user has a registered subscription and has not opted out of that notification type. Users can configure which types of push notifications they receive from Profile â†’ Notifications.

---

## Role Access

| Feature | All Roles | Admin/SuperAdmin Only |
|---|---|---|
| View in-app notifications | âœ“ | â€” |
| Mark notifications read | âœ“ | â€” |
| Configure own preferences | âœ“ | â€” |
| Manage any user's preferences | â€” | âœ“ |
| Send test push | âœ“ (self) | Required in production |

---

## Notification Types

| ID | Name |
|---|---|
| 1 | General |
| 2 | PaymentDue |
| 3 | PaymentReceived |
| 4 | CycleCreated |
| 5 | SystemRestart |

---

## Backend

### Controller: `NotificationController` â€” `/api/notification`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/notification` | Authorized | Get all notifications + unread count for current user |
| PUT | `/api/notification/{id}/read` | Authorized | Mark one notification as read |
| PUT | `/api/notification/read-all` | Authorized | Mark all notifications as read |

### Controller: `NotificationPreferenceController`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/notification-preferences` | Authorized | Get own preferences (all 5 types) |
| PUT | `/api/notification-preferences` | Authorized | Update own preferences |
| GET | `/api/admin/notification-preferences/{userId}` | AdminOrHigher | Get any user's preferences |
| PUT | `/api/admin/notification-preferences/{userId}` | AdminOrHigher | Update any type for any user |

### Controller: `PushController` â€” `/api/push`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/push/vapid-public-key` | Anonymous | Returns VAPID public key |
| POST | `/api/push/subscribe` | Authorized | Register or refresh a browser push subscription |
| DELETE | `/api/push/unsubscribe` | Authorized | Remove a push subscription by endpoint |
| POST | `/api/push/test` | Authorized (Admin in prod) | Send a test push notification to self |

#### `POST /api/push/subscribe` â€” Upsert Subscription
```
endpoint     string    Web Push endpoint URL
p256dh       string    Public key (base64)
auth         string    Auth secret (base64)
userAgent    string?   Browser user agent
```
Finds existing `PushSubscription` by `endpoint` or creates a new one. Updates the `Auth` and `P256dh` keys if they changed.

### Service: `NotificationService`

**File:** `server/src/Batanai.Api/Services/NotificationService.cs`

**Dependencies:** `ApplicationDbContext`

Operations:
- `CreateAsync(userId, message, type, deepLinkUrl?, relatedEntityId?)` â€” persists a `Notification` row.
- `GetForUserAsync(userId)` â€” returns `NotificationSummaryDto` with a list of notifications + total unread count.
- `MarkReadAsync(id, userId)` â€” sets `IsRead = true` for a single notification (validates ownership).
- `MarkAllReadAsync(userId)` â€” bulk update via `ExecuteUpdateAsync`.

### Service: `PushNotificationSender`

**File:** `server/src/Batanai.Api/Services/PushNotificationSender.cs`

**Dependencies:** `ApplicationDbContext`, `NotificationService`, VAPID settings, `HttpClient`

**`SendToUserAsync(userId, payload)`** â€” full pipeline:

1. **Always creates in-app notification** via `NotificationService.CreateAsync()` first.
2. **Preference check:** Queries `UserNotificationPreferences` for this user + notification type. If `IsEnabled = false`, skips push (in-app record still created).
3. **Load subscriptions:** Queries all `PushSubscription` rows for the user.
4. **Dispatch:** Sends VAPID-signed HTTP POST to each subscription endpoint using `Lib.Net.Http.WebPush`. All dispatches run in parallel (`Task.WhenAll`).
5. **Pruning:** Any subscription that returns HTTP 410 (Gone) or 404 is automatically deleted from the database.

**`SendToUsersAsync(userIds[], payload)`** â€” calls `SendToUserAsync` for each ID.

### Service: `NotificationPreferenceService`

**File:** `server/src/Batanai.Api/Services/NotificationPreferenceService.cs`

**`GetPreferencesAsync(userId)`**
- Loads all 5 `NotificationTypeEntity` rows.
- Joins with `UserNotificationPreference` rows for the user.
- For types with no stored preference row, defaults to `IsEnabled = true`.
- Returns a complete list of 5 `NotificationPreferenceDto` items.

**`UpdatePreferencesAsync(userId, items)`**
- Upserts the `UserNotificationPreference` row for each `(userId, typeId)` pair in the request.

### Model: `Notification`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK â†’ User |
| `Message` | string | Notification text |
| `IsRead` | bool | |
| `CreatedAt` | DateTime | |
| `Type` | NotificationType | Enum 1â€“5 |
| `DeepLinkUrl` | string? | Angular route to navigate on click |
| `RelatedEntityId` | int? | ID of the related entity (expense, payment, cycle) |
| `SentViaPush` | bool | Whether a push was dispatched |

### Model: `PushSubscription`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK â†’ User |
| `Endpoint` | string | Web Push endpoint URL |
| `P256dh` | string | Client public key |
| `Auth` | string | Auth secret |
| `UserAgent` | string? | |
| `CreatedAt` | DateTime | |

### Model: `UserNotificationPreference`

| Field | Type | Notes |
|---|---|---|
| `UserId` | int | PK (composite) |
| `NotificationTypeId` | int | PK (composite) FK â†’ NotificationTypeEntity |
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

**`subscribeToServer()`** â€” called on every login:
1. Checks `Notification.permission`; if not granted, requests permission.
2. Fetches VAPID public key from `GET /push/vapid-public-key`.
3. Calls `navigator.serviceWorker.ready` â†’ `registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: vapidKey })`.
4. Posts subscription to `POST /push/subscribe` with endpoint, p256dh, auth.

**`unsubscribeFromServer()`**
1. Gets current subscription from `pushManager.getSubscription()`.
2. Calls `subscription.unsubscribe()` (browser side).
3. Calls `DELETE /push/unsubscribe` with the endpoint.

**`sendTestPush(payload)`** â†’ `POST /push/test`.

### Notification Preferences Component

**File:** `client/src/app/features/profile/components/notification-preferences/notification-preferences.component.ts`
**Route:** `/profile/notifications`

- Loads `GET /notification-preferences` on init.
- All 5 notification types shown as toggle switches.
- **Save button:** Calls `PUT /notification-preferences` with the array of changed preferences.

### Admin: Per-User Notification Preferences

**File:** `client/src/app/features/management/components/user-management.component.ts`

- Bell icon button per user row in the User Management table.
- Opens a modal panel loaded with `GET /admin/notification-preferences/{userId}`.
- Displays all 5 notification types with individual toggle switches.
- Save calls `PUT /admin/notification-preferences/{userId}`.

---

## Notification Seeding on Registration

When a new user registers (`AuthService.SignupAsync`), all 5 `UserNotificationPreference` rows are inserted with `IsEnabled = true` to ensure the user receives all notifications by default until they explicitly opt out.

### Services

**`AudioAlarmService`** â€” `client/src/app/core/services/audio-alarm.service.ts`

- `unlock(ctx: AudioContext)` â€” call on user gesture to unblock iOS Safari. Plays a silent 1-sample buffer.
- `playAlarm()` â€” resumes the stored context, schedules 3 beeps, then returns. No-ops if muted or context is unavailable.
- `isMuted() / setMuted(val)` â€” reads/writes `localStorage` key `Batanai-alarm-muted`.

**`WakeLockService`** â€” `client/src/app/core/services/wake-lock.service.ts`

- `acquire()` â€” requests a screen wake lock; silently no-ops if the Wake Lock API is unsupported.
- `release()` â€” releases the active sentinel.
- `reacquire()` â€” called from `visibilitychange` handlers to re-request the lock after the tab returns to the foreground (OS revokes the sentinel on background).

### BGL Timer Persistence (Hypo + Ketone)

Both BGL timers now persist their scheduled expiry time and restore it on page reload.

| | Ketone 2-hr timer | Hypo 10/15-min timer |
|---|---|---|
| localStorage key | `bgl-ketone-timer` | `bgl-hypo-timer` |
| Server persistence | Yes â€” saved as `outcome: 5 MonitoringInProgress` | No |
| VAPID push scheduled | Yes â€” `POST /push/bg-timer/schedule` (120 min) | Yes â€” `POST /push/bg-timer/schedule` (10 or 15 min) |
| Wall-clock accuracy | Yes â€” `timerScheduledAtMs` anchor | Yes â€” same field, now set in `startTimer()` |

The `ServiceWorkerNotificationService` (`sw-notification.service.ts`) was extended with:
- `readonly HYPO_KEY = 'bgl-hypo-timer'` and `readonly KETONE_KEY = 'bgl-ketone-timer'` public fields.
- An optional `key?: string` parameter added to `scheduleNotification`, `saveTimerState`, `getTimerState`, `clearTimerState`, `hasActiveTimer`, and `getRemainingTime`. Default is `KETONE_KEY` â€” all existing callers are unaffected.

### BP Timer

The BP rest timer has the same alarm/vibration/wake lock experience but has no server persistence and no VAPID push (2 minutes is too short to warrant a background push).

---

## Service Worker Push Handling

When the browser receives a push event, the service worker (`client/src/custom-sw.js`) processes it:
1. Parses the push data payload (JSON: `{ title, body, deepLinkUrl?, type }`).
2. Calls `self.registration.showNotification(title, { body, data: { deepLinkUrl } })`.
3. On `notificationclick` event: focuses an existing Batanai window if open, or opens a new one, navigating to `deepLinkUrl` if provided.

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
  |                         | 410? â†’ delete subscription       |
  |                         |                                  |
  | Carer clicks notification|                                 |
  |                         |<-- notificationclick event ------|
  |                         |-- navigate to deepLinkUrl        |
  |                         |-- PUT /notification/{id}/read -->|
```
