# Service Worker Notifications

## Overview

Blueprint1989 uses a **custom Service Worker** (`custom-sw.js`) layered on top of Angular's generated `ngsw-worker.js` to handle push notification display and deep-link navigation. The service worker runs in the background and processes push events sent from the Blueprint1989 API via the Web Push Protocol.

---

## Table of Contents

1. [Architecture](#1-architecture)
2. [Push Event Handling](#2-push-event-handling)
3. [Notification Click Handling](#3-notification-click-handling)
4. [Subscription Change Handling](#4-subscription-change-handling)
5. [IndexedDB Usage](#5-indexeddb-usage)
6. [Testing](#6-testing)
7. [Troubleshooting](#7-troubleshooting)

---

## 1. Architecture

```
Blueprint1989 API (blueprint1989api.elroitec.com)
    ¦
    ¦  Web Push Protocol (RFC 8030)
    ¦  VAPID-authenticated POST to push endpoint
    ?
Browser Push Service (e.g. FCM, Mozilla)
    ¦
    ¦  push message delivered to browser
    ?
custom-sw.js  (Service Worker)
    ¦
    +-- push event  --? showNotification()
    ¦
    +-- notificationclick  --? clients.openWindow(deepLinkUrl)
    ¦
    +-- pushsubscriptionchange  --? re-subscribe + POST /api/push/subscribe
```

The custom service worker imports the Angular NGSW worker to preserve offline caching and app-update behaviour:

```javascript
importScripts('./ngsw-worker.js');
```

---

## 2. Push Event Handling

When the API sends a push message, the service worker fires the `push` event. The payload is a JSON object with these fields:

| Field         | Type   | Description                                    |
|---------------|--------|------------------------------------------------|
| `type`        | number | Notification type enum (1–5)                   |
| `title`       | string | Notification title                             |
| `body`        | string | Notification body text                         |
| `deepLinkUrl` | string | In-app route to open on click (optional)       |

### Code (custom-sw.js)

```javascript
self.addEventListener('push', event => {
  if (!event.data) return;

  const data = event.data.json();
  const { title, body, deepLinkUrl, type } = data;

  const options = {
    body,
    icon: '/assets/icons/icon-192x192.png',
    badge: '/assets/icons/badge-72x72.png',
    data: { deepLinkUrl },
    requireInteraction: isPriorityType(type),
  };

  event.waitUntil(
    self.registration.showNotification(title, options)
  );
});

function isPriorityType(type) {
  return [].includes(type); // no priority types currently defined
}
```

### Notification Types

| ID | Name              | Description                          |
|----|-------------------|--------------------------------------|
| 1  | General           | General-purpose system messages      |
| 2  | PaymentDue        | A payment obligation is outstanding  |
| 3  | PaymentReceived   | A payment has been confirmed         |
| 4  | CycleCreated      | A new expense cycle was opened       |
| 5  | SystemRestart     | Server restart / maintenance notice  |

---

## 3. Notification Click Handling

When a user taps a notification, the `notificationclick` event fires. The service worker closes the notification and navigates to the `deepLinkUrl` (if provided), or focuses an existing app window.

```javascript
self.addEventListener('notificationclick', event => {
  event.notification.close();

  const deepLinkUrl = event.notification.data?.deepLinkUrl;
  const targetUrl = deepLinkUrl
    ? new URL(deepLinkUrl, self.location.origin).href
    : self.location.origin;

  event.waitUntil(
    clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windowClients => {
      for (const client of windowClients) {
        if (client.url === targetUrl && 'focus' in client) {
          return client.focus();
        }
      }
      return clients.openWindow(targetUrl);
    })
  );
});
```

### Deep Link Examples

| Notification Type | deepLinkUrl example              |
|-------------------|----------------------------------|
| PaymentDue        | `/cycles/3/obligations`          |
| PaymentReceived   | `/cycles/3/payments`             |
| CycleCreated      | `/cycles/5`                      |
| General           | `/notifications`                 |

---

## 4. Subscription Change Handling

If the push subscription expires or is rotated by the browser, the `pushsubscriptionchange` event fires. The service worker re-subscribes and sends the new subscription to the API.

```javascript
self.addEventListener('pushsubscriptionchange', event => {
  const applicationServerKey = urlBase64ToUint8Array('<VAPID_PUBLIC_KEY>');

  event.waitUntil(
    self.registration.pushManager.subscribe({
      userVisibleOnly: true,
      applicationServerKey,
    }).then(subscription => {
      return fetch('/api/push/subscribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(subscription),
        credentials: 'include',
      });
    })
  );
});
```

> **Note:** The VAPID public key is injected at build time via Angular environment variables. See [App-Configuration.md](App-Configuration.md) for details.

---

## 5. IndexedDB Usage

The Blueprint1989 service worker does **not** use IndexedDB for notification scheduling. Push notifications are server-initiated — the API triggers them when business events occur (payment recorded, cycle created, etc.). There is no client-side timer or local notification queue.

Angular NGSW uses its own internal IndexedDB (`ngsw`) for caching; this is managed automatically and does not require manual intervention.

---

## 6. Testing

### Using the Dev Push Test Page

Navigate to `/dev/push-test` in the Blueprint1989 app (available in development builds). Enter a notification type and message, then click **Send Test Push** to trigger a push via `POST /api/push/test`.

### Using curl / Postman

```http
POST https://blueprint1989api.elroitec.com/api/push/test
Authorization: Bearer <token>
Content-Type: application/json

{
  "type": 2,
  "title": "Payment Due",
  "body": "You owe Jane £25.00 in cycle January 2026.",
  "deepLinkUrl": "/cycles/1/obligations"
}
```

### Verifying in DevTools

1. Open **Application ? Service Workers** in Chrome DevTools.
2. Confirm `custom-sw.js` status is **Activated and running**.
3. Click **Push** (with a JSON payload) to simulate a push event without going through the API.
4. The notification should appear within 1–2 seconds.

### Inspecting Registered Subscriptions

```http
GET /api/push/subscriptions
Authorization: Bearer <admin-token>
```

Returns the list of `UserPushSubscription` rows for all users.

---

## 7. Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Notifications not appearing | Permission blocked in browser | Ask user to allow notifications in Site Settings |
| Notification shows but click does nothing | `deepLinkUrl` is null or incorrect | Verify the API is populating `deepLinkUrl` in the push payload |
| Subscription expires silently | `pushsubscriptionchange` not re-subscribing | Check that the VAPID public key in service worker matches `appsettings.json` |
| Service worker stuck on "waiting to activate" | Old NGSW tab still open | Close all app tabs and reload |
| Push delivers but no notification shown | Service worker event handler not registered | Confirm `custom-sw.js` is imported via `ngsw-config.json` `custom-sw.js` property |

---

*For full push notification architecture including server-side components, see [push-notifications.md](push-notifications.md).*
*For VAPID key generation and rotation, see [push-notifications-reference.md](push-notifications-reference.md).*
