# Push Notifications

## Overview

The Push Notifications system delivers real-time alerts to users via the **Web Push** protocol using **VAPID** (Voluntary Application Server Identification). It comprises three layers: a server-side push pipeline (notification creation, preference check, subscription dispatch), a client-side subscription manager, and a service worker push handler that renders notifications even when the app is not in the foreground. Additionally, a background timer feature allows carers to schedule a one-off push reminder for a specific care recipient.

---

## Architecture Overview

```
Event occurs (e.g., BP Incident with Stage 2 classification)
  → PushNotificationSender.SendAsync(recipientUserId, notificationType, payload)
     1. Creates in-app Notification row in DB
     2. Loads NotificationPreference for user + type
     3. If preference disabled → stop
     4. Loads all PushSubscriptions for user
     5. For each subscription: sends VAPID push via WebPush library
        → HTTP 410 Gone: subscription removed from DB (pruned)
        → HTTP 4xx/5xx: logged, continue to next subscription

Client (browser / service worker)
  → SW receives 'push' event
  → Parses payload JSON
  → Calls self.registration.showNotification(title, options)
  → On 'notificationclick': opens app URL (if provided in payload)
```

---

## Backend

### Controller — `PushController`

**File:** `server/src/Batanai.Api/Controllers/PushController.cs`

Base route: `/api/push`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/vapid-public-key` | Public | Returns the VAPID public key for client subscription |
| POST | `/subscribe` | `AllRoles` | Saves a new push subscription for the current user |
| DELETE | `/unsubscribe` | `AllRoles` | Removes a subscription by endpoint URL |
| POST | `/test` | `AllRoles` | Sends a test push to the current user's subscriptions |
| POST | `/bg-timer/schedule` | `AllRoles` | Schedules a background timer reminder |
| DELETE | `/bg-timer/cancel/{id}` | `AllRoles` | Cancels a pending background timer |

#### Subscribe

Accepts a `PushSubscriptionDto`:

```json
{
  "endpoint": "https://fcm.googleapis.com/fcm/send/...",
  "p256dh": "base64-encoded-key",
  "auth": "base64-encoded-auth"
}
```

Deduplicates by `endpoint` — if already exists for the user, updates the keys in place.

#### Unsubscribe

Accepts `{ "endpoint": "..." }`. Deletes the subscription row matching the endpoint for the current user.

#### Test Push

Sends a push immediately with payload:
```json
{ "title": "Test Notification", "body": "Push notifications are working!" }
```

---

### Model — `PushSubscription`

**File:** `server/src/Batanai.Api/Models/PushSubscription.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `UserId` | `int` | FK → User |
| `Endpoint` | `string` | Browser push endpoint URL |
| `P256dh` | `string` | Client public key (Base64url) |
| `Auth` | `string` | Client auth secret (Base64url) |
| `DeviceName` | `string?` | Optional friendly name |
| `CreatedAt` | `DateTime` | Row created |
| `User` | `User` | Navigation property |

---

### Service — `PushNotificationSender`

**File:** `server/src/Batanai.Api/Services/PushNotificationSender.cs`

The `PushNotificationSender` is the central dispatch service used by all features that emit push notifications.

#### `SendAsync(userId, notificationType, title, body, data?)`

Full 5-step pipeline:

**Step 1 — Create In-App Notification**
Inserts a `Notification` row into the database (see [Notifications.md](Notifications.md)).

**Step 2 — Load Preference**
Calls `NotificationPreferenceService.GetPreferenceAsync(userId, notificationType)`.
- If the preference is disabled (`IsEnabled = false`) → method returns early. No push is sent.

**Step 3 — Load Subscriptions**
Queries all `PushSubscription` rows for the target `userId`.
- If the user has no subscriptions → returns early.

**Step 4 — Send VAPID Push**
For each subscription, constructs a `WebPushPayload`:
```json
{
  "title": "...",
  "body": "...",
  "data": { "url": "/incidents?cr=5", ... }
}
```
Sends via the **WebPush** library using the VAPID private key loaded from `AppConfig` (`VAPID_PRIVATE_KEY`, `VAPID_SUBJECT`).

**Step 5 — Prune Dead Subscriptions**
If the push service returns **HTTP 410 Gone** (subscription expired/removed by the browser), the subscription row is deleted from the database. Other HTTP errors (400, 413) are logged but do not delete the row.

---

### Background Timer — `BgTimerHostedService`

**File:** `server/src/Batanai.Api/Services/BgTimerHostedService.cs`

A hosted background service that polls for scheduled push reminders.

#### Behaviour

- Polls every **30 seconds** (`Timer` interval).
- Queries all `BgTimer` rows where `ScheduledAt <= DateTime.UtcNow AND SentAt IS NULL`.
- For each: calls `PushNotificationSender.SendAsync(timer.UserId, "BgTimerReminder", ...)`.
- Sets `SentAt = DateTime.UtcNow` on the row to prevent re-sending.

#### Schedule Endpoint — `POST /push/bg-timer/schedule`

Request body:
```json
{
  "careRecipientId": 5,
  "minutesFromNow": 15,
  "message": "Check BGL for Jane"
}
```

Creates a `BgTimer` row: `ScheduledAt = DateTime.UtcNow.AddMinutes(minutesFromNow)`.

#### Cancel Endpoint — `DELETE /push/bg-timer/cancel/{id}`

Sets `CancelledAt = DateTime.UtcNow` on the row (soft cancel). The hosted service skips rows with a non-null `CancelledAt`.

#### BgTimer Model

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `UserId` | `int` | FK → User (push target) |
| `CareRecipientId` | `int?` | Context CR |
| `Message` | `string` | Custom reminder text |
| `ScheduledAt` | `DateTime` | When to send |
| `SentAt` | `DateTime?` | Populated after send |
| `CancelledAt` | `DateTime?` | Populated if cancelled |

---

### VAPID Configuration

Keys are stored in `AppConfig`:

| AppConfig Key | Description |
|---|---|
| `VAPID_PUBLIC_KEY` | Base64url-encoded VAPID public key |
| `VAPID_PRIVATE_KEY` | Base64url-encoded VAPID private key (secret) |
| `VAPID_SUBJECT` | `mailto:` or `https:` contact URI |

The public key is served publicly via `GET /api/push/vapid-public-key` because the browser needs it at subscription time.

The private key is never sent to the client.

Keys are generated once using the `vapid-keygen` utility project at `server/vapid-keygen/`.

---

## Frontend

### `PushNotificationService`

**File:** `client/src/app/core/services/push-notification.service.ts`

#### `subscribeToServer()`

Called on every successful login (from `LoginComponent` after token is received).

Full flow:
1. Checks `'PushManager' in window` — if not supported, stops silently.
2. Gets the service worker registration: `navigator.serviceWorker.ready`.
3. Checks current permission: if `'denied'` → stops.
4. Calls `pushManager.getSubscription()`.
   - If subscription exists: calls `POST /push/subscribe` with existing subscription (re-registering is idempotent).
   - If no subscription: requests permission via `Notification.requestPermission()`.
     - Permission granted: `pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: vapidPublicKey })`.
     - Posts to `POST /push/subscribe` with new subscription.
     - Permission denied: stores a flag in localStorage to avoid re-asking.
5. VAPID public key is loaded from `GET /api/push/vapid-public-key` on app init and cached.

#### `unsubscribe()`

Called when the user disables push notifications in `NotificationPreferencesComponent` or disconnects a device.

1. Gets current subscription: `pushManager.getSubscription()`.
2. Calls `subscription.unsubscribe()` (browser level).
3. Calls `DELETE /push/unsubscribe` with the endpoint URL.

#### `sendTestPush()`

Calls `POST /push/test`. Used in `NotificationPreferencesComponent` to verify push delivery.

---

### `BgTimerComponent`

**File:** `client/src/app/features/shared/bg-timer/bg-timer.component.ts` (or embedded in assessment/incident pages)

Accessible from BGL and BP entry flows as a "Set Reminder" button.

#### Form Controls

| Control | Type | Description |
|---|---|---|
| `minutesFromNow` | Number input | Minutes until reminder fires |
| `message` | Textarea | Custom reminder text |

On submit: calls `POST /push/bg-timer/schedule`.

Active timers displayed with a countdown. "Cancel" button calls `DELETE /push/bg-timer/cancel/{id}`.

---

### Service Worker Push Handler

**File:** `client/src/custom-sw.js`

The service worker listens for the `push` event and renders a system notification:

```js
self.addEventListener('push', event => {
  const data = event.data?.json() ?? {};
  const title = data.title ?? 'Batanai';
  const options = {
    body: data.body ?? '',
    icon: '/assets/icons/icon-192x192.png',
    badge: '/assets/icons/badge-72x72.png',
    data: { url: data.data?.url ?? '/' },
    vibrate: [200, 100, 200]
  };
  event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  const url = event.notification.data?.url ?? '/';
  event.waitUntil(clients.openWindow(url));
});
```

The `notificationclick` handler opens the app and navigates to the `url` embedded in the notification payload (e.g., `/incidents?careRecipientId=5`).

---

## Notification Types That Trigger Pushes

| Notification Type | Trigger | Recipients |
|---|---|---|
| `LinkedAsCarer` | Carer successfully linked to CR | Carer + CR |
| `LinkedAsCareRecipient` | Same event | Both users |
| `DelinkRequestSubmitted` | CR submits delink request | Admin + CR |
| `DelinkRequestApproved` | Admin approves delink | Carer + CR |
| `DelinkRequestRejected` | Admin rejects delink | CR |
| `DiabetesIncidentRecorded` | Incident created | Admin + CR |
| `BpIncidentRecorded` | BP incident created | Admin + CR |
| `LowBglAlert` | Hypo classification on incident | Admin + CR |
| `HighBglAlert` | Hyper classification on incident | Admin + CR |
| `BpHypotensionAlert` | Hypotension on BP incident | Admin + CR |
| `BpHighAlert` | Stage 2 or Crisis BP | Admin + CR |
| `BgTimerReminder` | Scheduled timer fires | Requesting user |
| `FeatureBugReportSubmitted` | User submits report | SuperAdmin |
| `SupplyStockLow` | Weekly report finds supplies running low | Admin |

---

## End-to-End Push Flow

```
1. User logs in
   → PushNotificationService.subscribeToServer()
   → Browser prompts for notification permission
   → SW pushManager.subscribe({ applicationServerKey: vapidPublicKey })
   → POST /push/subscribe { endpoint, p256dh, auth }
   → Server saves PushSubscription row

2. Clinical event occurs (e.g., BP incident with Stage 2)
   → BpIncidentService creates incident
   → PushNotificationSender.SendAsync(adminId, "BpHighAlert", ...)
     → Notification row created in DB (in-app bell)
     → Preference checked → enabled
     → Load PushSubscription rows for admin
     → WebPush.SendNotificationAsync(endpoint, p256dh, auth, payload, VAPID)
     → Push service delivers to browser

3. Browser receives push (app may be closed)
   → Service worker 'push' event fires
   → self.registration.showNotification("High BP Alert", { body: "..." })
   → System tray / lock screen notification shown

4. User taps notification
   → SW 'notificationclick' fires
   → clients.openWindow("/bp-incidents?careRecipientId=5")
   → App opens at relevant page
```
