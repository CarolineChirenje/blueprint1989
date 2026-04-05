# Push Notifications

Batanai uses the **Web Push Protocol** (RFC 8030) with **VAPID authentication** to deliver real-time push notifications from the server to any browser where a user is subscribed — even when the app is not open.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [How It Works End-to-End](#2-how-it-works-end-to-end)
3. [Notification Types](#3-notification-types)
4. [Server Components](#4-server-components)
5. [Client Components](#5-client-components)
6. [VAPID Keys](#6-vapid-keys)
7. [Testing Push Notifications](#7-testing-push-notifications)
8. [How to Add a New Push Notification](#8-how-to-add-a-new-push-notification)

---

## 1. Architecture Overview

```
+------------------------------------------------------+
¦                   Browser / PWA                      ¦
¦                                                      ¦
¦  Angular App  ?---- In-app Notification Bell         ¦
¦       ¦                                              ¦
¦  PushNotificationService                             ¦
¦       ¦                                              ¦
¦  custom-sw.js (Service Worker)                       ¦
¦       ?                                              ¦
¦       ¦  OS-level push event                         ¦
+-------+----------------------------------------------+
        ¦
   Browser Push Service (FCM / APNS / Mozilla)
        ?
        ¦  VAPID-signed HTTP request
        ¦
+------------------------------------------------------+
¦                  .NET 10 API                         ¦
¦                                                      ¦
¦  Any Controller / Service                            ¦
¦       ¦                                              ¦
¦  IPushNotificationSender  ?-- single seam            ¦
¦       ¦                                              ¦
¦  PushNotificationSender                              ¦
¦   • Creates in-app Notification row                  ¦
¦   • Delivers VAPID push to all subscribed devices    ¦
¦                                                      ¦
¦  BgTimerHostedService (BackgroundService)            ¦
¦   • Polls every 30 s for due BgTimerReminders        ¦
+------------------------------------------------------+
```

**Key design principles:**

- `IPushNotificationSender` is the **single extension seam** — to add any new push, call this interface.
- Every push **also saves an in-app `Notification` row** so the bell icon always reflects the same events, even if the browser blocks push.
- The VAPID public key is **fetched at runtime** from `GET /api/push/vapid-public-key` — it is never baked into the Angular build.
- A single `custom-sw.js` file handles both local timer logic and push events — no `@angular/service-worker` conflict.

---

## 2. How It Works End-to-End

### Subscription flow (once per user per device)

1. User logs in. `AppComponent.initPush()` triggers.
2. `PushNotificationService.subscribeToServer()`:
   - Calls `GET /api/push/vapid-public-key` to get the VAPID public key.
   - Calls `PushManager.subscribe({ userVisibleOnly: true, applicationServerKey })` — browser prompts for permission on first run.
   - POSTs the resulting `PushSubscription` (endpoint + keys) to `POST /api/push/subscribe`.
3. The server stores the subscription in the `PushSubscriptions` table (upsert by `UserId + Endpoint`).

### Delivery flow (for every trigger)

1. Something happens (incident saved, delink approved, carer removed, etc.).
2. The responsible controller/service calls `IPushNotificationSender.SendToUserAsync(...)` or `SendToUsersAsync(...)`.
3. `PushNotificationSender`:
   - Inserts an in-app `Notification` row via `NotificationService.CreateAsync(...)`.
   - Loads all `PushSubscription` rows for the target user(s).
   - Sends a VAPID-signed HTTP request to the browser push service (Google FCM, Mozilla, etc.) for each subscription in parallel.
   - If the push service responds `410 Gone`, the subscription is expired and is automatically deleted.
4. The browser's service worker (`custom-sw.js`) receives the `push` event and calls `self.registration.showNotification(title, options)`.
5. Clicking the notification fires the `notificationclick` event, which navigates the tab to the `deepLinkUrl` embedded in the notification data.

---

## 3. Notification Types

Each notification type maps to a row in the **`NotificationTypes`** lookup table. The integer value is the **primary key** of that table and is always sent inside the push payload so the service worker knows how to present it.

| Id | Enum Name | Trigger | Priority* |
|----|-----------|---------|----------|
| 1 | `General` | Manual test / generic | No |
| 2 | `PaymentDue` | Cycle closes — obligation calculated | No |
| 3 | `PaymentReceived` | Payment confirmed by creditor or admin | No |
| 4 | `CycleCreated` | New expense cycle created | No |
| 5 | `SystemRestart` | Admin triggers system restart | No |

\* **Priority** notifications use `requireInteraction: true` — they stay on screen until the user dismisses them.

---

## 4. Server Components

### Models

| File | Purpose |
|------|---------|
| `Models/NotificationType.cs` | C# enum — source of truth for integer values |
| `Models/NotificationTypeEntity.cs` | EF lookup table entity (`NotificationTypes`) |
| `Models/PushSubscription.cs` | Stores one browser subscription per user per device |
| `Models/Notification.cs` | In-app notification (enriched with `Type`, `DeepLinkUrl`, `SentViaPush`) |

### Services

| File | Purpose |
|------|---------|
| `Services/IPushNotificationSender.cs` | Abstraction — the single seam for all push triggers |
| `Services/PushNotificationSender.cs` | VAPID implementation using `Lib.Net.Http.WebPush` |
| `Services/VapidSettings.cs` | POCO bound to the `"Vapid"` config section |
| `Services/NotificationService.cs` | Creates in-app notification rows (called inside `PushNotificationSender`) |

### Controller

`Controllers/PushController.cs` — all `api/push/*` endpoints:

| Method | Route | Auth | Purpose |
|--------|-------|------|---------|
| `GET`    | `/api/push/vapid-public-key`            | Anonymous | Returns the VAPID public key for client subscription |
| `POST`   | `/api/push/subscribe`                   | Authenticated | Upserts a `PushSubscription` for the current user |
| `DELETE` | `/api/push/unsubscribe`                 | Authenticated | Removes a subscription |
| `POST`   | `/api/push/test`                        | Dev: any auth; Prod: Admin+ | Sends a test push to the current user |

### Dependency injection (`Program.cs`)

```csharp
builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
builder.Services.AddHttpClient<PushNotificationSender>();
builder.Services.AddScoped<IPushNotificationSender, PushNotificationSender>();
```

---

## 5. Client Components

| File | Purpose |
|------|---------|
| `core/services/push-notification.service.ts` | Manages browser subscription and exposes push API methods |
| `core/guards/dev-only.guard.ts` | Blocks the test panel route in production |
| `features/push-test/push-test.component.*` | Dev-only test panel at `/dev/push-test` |
| `custom-sw.js` | Service worker — handles `push`, `pushsubscriptionchange` and `notificationclick` events |

### `PushNotificationService` public API

```typescript
requestPermission(): Promise<NotificationPermission>
subscribeToServer(): Promise<boolean>
unsubscribeFromServer(): Promise<void>
sendTestPush(req: TestPushRequest): Observable<void>
```

### `NotificationType` enum (client)

Defined in `push-notification.service.ts` and must always mirror the server enum values:

```typescript
export enum NotificationType {
  General          = 1,
  PaymentDue       = 2,
  PaymentReceived  = 3,
  CycleCreated     = 4,
  SystemRestart    = 5,
}
```

---

## 6. VAPID Keys

VAPID (Voluntary Application Server Identification) keys are used to sign push requests so push services can verify they come from this server.

### Development keys

Set in `appsettings.Development.json`:

```json
"Vapid": {
  "Subject": "mailto:dev@Batanai.local",
  "PublicKey": "BMHIw8zld_obcwRmht3TiYhm10cXYpoiM24K8uqafdaIRMG8jc0d9Cb3BxdV1SCNi4An_Fhqut-AdyurBDYRNnI",
  "PrivateKey": "l1i0nQU5lvhatuTrDcze-jCD9FNFaq5RECn42Zm8nIM"
}
```

### Production keys

`appsettings.json` holds placeholder values — **replace these before deploying**:

```json
"Vapid": {
  "Subject": "mailto:admin@Batanai.com",
  "PublicKey": "REPLACE_WITH_YOUR_VAPID_PUBLIC_KEY",
  "PrivateKey": "REPLACE_WITH_YOUR_VAPID_PRIVATE_KEY"
}
```

To generate a new key pair, use the one-time keygen console app in `server/vapid-keygen/` or any standard VAPID key generator.

> ?? **Never commit production VAPID keys to source control.** Use environment variables or a secrets manager in production.

---

## 7. Testing Push Notifications

A developer-only test panel is available at **`/dev/push-test`** (blocked by `DevOnlyGuard` in production).

**Steps:**

> **Important:** `ng serve` bypasses the Angular service worker entirely. Push notifications require the SW to be active, so you must use `ng build` + a static file server instead.

1. Start the API: `dotnet run` in `server/src/Batanai.Api/`
2. Build and serve the client:
   ```powershell
   cd client
   npx ng build
   http-server dist/Batanai/browser -p 4200 -c-1
   ```
3. Navigate to `http://localhost:4200/dev/push-test`
4. **Step 1 — Permission:** Click *Request Permission* ? allow in the browser prompt.
5. **Step 2 — Subscribe:** Click *Subscribe* ? confirm the  Subscribed status appears.
6. **Step 3 — Send test push:** Choose a notification type, fill in a title and body, optionally enter a deep-link URL, then click *?? Send Test Push*.
7. The OS notification should appear within seconds. Clicking it navigates to the deep-link URL.

You can also use the API directly:

```http
POST /api/push/test
Authorization: Bearer <token>
Content-Type: application/json

{
  "type": 2,
  "title": "Payment Due",
  "body": "You owe Jane £25.00 in cycle January 2026.",
  "deepLinkUrl": "/cycles/1/obligations"
}
```

---

## 8. How to Add a New Push Notification

The system is designed so that adding a new notification type is a small, well-contained change. Follow these steps:

### Step 1 — Add the enum value (server)

Open `server/src/Batanai.Api/Models/NotificationType.cs` and add the new value with the next sequential integer:

```csharp
/// <summary>Member expense summary reminder.</summary>
ExpenseSummary = 6,
```

### Step 2 — Add the enum value (client)

Open `client/src/app/core/services/push-notification.service.ts` and add the matching value to the `NotificationType` TypeScript enum:

```typescript
ExpenseSummary = 6,
```

> The integer values **must match** between server and client — the raw integer is what travels inside the push payload.

### Step 3 — Seed the lookup table row

Open `server/src/Batanai.Api/Data/ApplicationDbContext.cs`, find `ConfigureNotificationTypeEntity()`, and add a seed row:

```csharp
new NotificationTypeEntity { Id = 6, Name = "ExpenseSummary", Description = "Member expense summary reminder" }
```

### Step 4 — Create an EF migration

```powershell
cd server/src/Batanai.Api
dotnet ef migrations add AddExpenseSummaryNotificationType
dotnet ef database update
```

### Step 5 — Mark as priority (optional)

If this notification should require user interaction (stay on screen until dismissed), open `client/src/custom-sw.js` and add the new integer to `isPriorityType`:

```javascript
function isPriorityType(type) {
  return [].includes(type); // no priority types currently defined
}
```

### Step 6 — Trigger the notification

In the controller or service where the event occurs, inject `IPushNotificationSender` and call it:

```csharp
// Single user
await _pushSender.SendToUserAsync(
    userId: memberUserId,
    type:   NotificationType.PaymentDue,
    title:  "Payment Due",
    body:   $"You owe {creditor.DisplayName} £{amount:F2} in cycle {cycle.Name}.",
    deepLinkUrl: $"/cycles/{cycle.Id}/obligations"
);

// Multiple users
await _pushSender.SendToUsersAsync(
    userIds: cycleMembers,
    type:    NotificationType.CycleCreated,
    title:   "New Cycle Started",
    body:    $"Cycle '{cycle.Name}' has been opened by {admin.DisplayName}.",
    deepLinkUrl: $"/cycles/{cycle.Id}"
);
```

That's all. The in-app `Notification` row is created automatically, the push is delivered to all subscribed devices, and the `NotificationTypes` lookup table already has the new row.

### Step 7 — Add to the Dev test panel (optional but recommended)

Open `client/src/app/features/push-test/push-test.component.ts` and add the type to `notificationTypes`:

```typescript
{ value: NotificationType.ExpenseSummary, label: 'Expense Summary' },
```

---

### Summary checklist for a new notification type

| # | What | Where |
|---|------|-------|
| 1 | Add enum value (next integer) | `Models/NotificationType.cs` |
| 2 | Add matching TypeScript enum value | `push-notification.service.ts` |
| 3 | Add `HasData` seed row | `ApplicationDbContext.ConfigureNotificationTypeEntity()` |
| 4 | Generate + apply EF migration | `dotnet ef migrations add ...` & `dotnet ef database update` |
| 5 | Add to `isPriorityType` (if needed) | `custom-sw.js` |
| 6 | Call `IPushNotificationSender` from trigger | Relevant controller or service |
| 7 | Add to Dev test panel (optional) | `push-test.component.ts` |

