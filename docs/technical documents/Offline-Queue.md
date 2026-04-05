# Offline Queue

## Overview

The Offline Queue enables the application to capture expense and payment entries when the device has no internet connectivity, then automatically (or manually) synchronise them to the server once connectivity is restored. It uses the browser's **IndexedDB** as a persistent local store, with a 24-hour time-to-live (TTL) per entry so stale data is not silently submitted after prolonged disconnection.

Two data types are supported in the queue:

| Type Key | Feature |
|---|---|
| `expense` | Expense Entry |
| `payment` | Payment Submission |

---

## Architecture

```
User submits form (offline)
  → Feature component calls OfflineQueueService.enqueue(type, payload)
  → Stored in IndexedDB store "offlineQueue" with timestamp
  → Toast: "Saved offline. Will sync when back online."

Connectivity restored
  → App detects "online" event OR user opens Offline Queue page
  → SyncService.syncPending() replays each entry to the correct API
  → On success: entry removed from IndexedDB
  → SW posts OFFLINE_SYNC_COMPLETE event to all clients
  → Toast: "X entries synced successfully."
```

---

## Frontend — `OfflineQueueService`

**File:** `client/src/app/core/services/offline-queue.service.ts`

### IndexedDB Store

- **Database name:** `BatanaiDb`.
- **Store name:** `offlineQueue`.
- **Key:** auto-increment integer.

### Entry Schema

```ts
interface OfflineQueueEntry {
  id?: number;           // Auto-assigned by IndexedDB
  type: 'expense' | 'payment';
  payload: unknown;      // The full DTO that would have been POSTed
  createdAt: number;     // Unix timestamp (ms) — used for TTL checks
  retryCount: number;    // Incremented on each failed sync attempt
}
```

### 24-Hour TTL

On every `syncPending()` call (and optionally on page load), entries older than 24 hours are moved to "Expired" status and are no longer auto-synced. They remain visible in the UI until the user explicitly discards them.

TTL check: `Date.now() - entry.createdAt > 24 * 60 * 60 * 1000`

### Key Methods

| Method | Description |
|---|---|
| `enqueue(type, payload)` | Adds an entry to IndexedDB |
| `getPending()` | Returns all entries where `createdAt` is within 24 hours |
| `getExpired()` | Returns all entries older than 24 hours |
| `remove(id)` | Deletes a specific entry by IndexedDB key |
| `clearExpired()` | Bulk-deletes all expired entries |
| `getCount()` | Returns total pending entry count (used for badge) |

---

## Frontend — `SyncService`

**File:** `client/src/app/core/services/sync.service.ts`

The `SyncService` orchestrates the replay of queued entries against the live API.

### `syncPending()`

1. Calls `OfflineQueueService.getPending()` to retrieve all non-expired entries.
2. For each entry, dispatches to the correct HTTP endpoint based on `type`:

   | Type | Endpoint |
   |---|---|
   | `expense` | `POST /api/expense` |
   | `payment` | `POST /api/payment` |

3. On **success** (HTTP 2xx): calls `OfflineQueueService.remove(entry.id)`.
4. On **failure**: increments `retryCount`. If `retryCount >= 3`, marks the entry as permanenly failed (left in expired state rather than retried again automatically).
5. After processing all entries, broadcasts an `OFFLINE_SYNC_COMPLETE` custom event (or service worker `postMessage`) to all active clients.
6. Triggers a toast notification summarising synced / failed counts.

### Auto-Sync on Reconnect

`AppComponent` (or the service itself) listens for the global `online` event:

```ts
window.addEventListener('online', () => this.syncService.syncPending());
```

This means every time network connectivity is restored, pending entries are automatically replayed without any user action.

---

## Frontend — Service Worker Integration

The service worker (`custom-sw.js`) intercepts failed POST requests to the API when offline and posts a `QUEUE_ITEM` message to the Angular app, which then calls `OfflineQueueService.enqueue()`. This allows network errors at the HTTP level to be captured even outside the normal form submission flow.

When `SyncService.syncPending()` completes, it dispatches a `postMessage` to the service worker with `{ type: 'OFFLINE_SYNC_COMPLETE' }`. The service worker can then broadcast this to all open clients, allowing every tab/window to show the sync completion notification.

---

## Frontend — `OfflineQueueComponent`

**File:** `client/src/app/features/offline-queue/offline-queue.component.ts`

Route: `/offline-queue` (accessible from Dashboard)

### Layout

Two sections:

#### Pending Section

Lists all non-expired entries:
- Icon for data type.
- Data type label (e.g., "BGL Assessment").
- Time queued (formatted as "X minutes ago" or specific date-time).
- Care Recipient name (if available in payload).
- Discard button (removes single entry with confirmation).

"Sync Now" button at the top:
- Calls `SyncService.syncPending()`.
- Shows a spinner while in progress.
- Shows per-entry success/failure feedback.

#### Expired Section

Lists all entries older than 24 hours:
- Same display as pending but with a red "Expired" badge.
- Individual discard buttons.
- "Clear All Expired" button — calls `OfflineQueueService.clearExpired()`.

### Queue Badge

`AppComponent` (header / sidenav) shows a badge on the offline-queue nav item with the count of pending entries, refreshed whenever the count changes.

---

## Feature Component Integration

Each form component that supports offline submission follows this pattern:

```ts
async onSubmit() {
  try {
    await this.featureService.create(formValue).toPromise();
    this.showSuccess();
  } catch (err) {
    if (!navigator.onLine) {
      await this.offlineQueueService.enqueue('incident', formValue);
      this.showOfflineSaveToast();
    } else {
      this.showError(err);
    }
  }
}
```

Components with offline support:
- `BglReadingComponent` (assessment)
- `IncidentComponent` (incident)
- `MealEntryComponent` (meal-entry)
- `BpEntryComponent` (blood-pressure)
- `BpIncidentFormComponent` (bp-incident)

---

## Data Integrity Considerations

- **TTL enforcement:** The 24-hour TTL prevents submission of clinically stale data (e.g., a BGL reading from yesterday being submitted as "now").
- **Idempotency:** The backend does not have idempotency guards on POST endpoints; if a sync is interrupted midway, a duplicate entry may appear. Users are expected to review and discard any duplicates.
- **Sensitive data at rest:** Payloads stored in IndexedDB are not encrypted. IndexedDB data is origin-scoped and not accessible by other origins, but the device must be considered trusted (shared or stolen device is a risk vector).
- **Token expiry:** If the JWT expires while entries are queued, the sync will fail with 401. The `AuthInterceptor` handles token refresh, but if the refresh token is also expired, entries will remain pending until the user logs in again.

---

## End-to-End Data Flow

```
Form submitted while offline
  → OfflineQueueService.enqueue(type, payload)
  → IndexedDB write: { type, payload, createdAt: now, retryCount: 0 }
  → Feature component shows "Saved offline" toast
  → Queue badge count increments in header

Device comes back online
  → window "online" event fires
  → SyncService.syncPending()
    → getPending() → filter by TTL < 24h
    → For each: POST /api/{endpoint} with payload + auth header
      → 2xx: OfflineQueueService.remove(id)
      → Error: increment retryCount, skip if >= 3
  → SW postMessage: OFFLINE_SYNC_COMPLETE
  → Toast: "3 entries synced"
  → Queue badge count decrements

User opens /offline-queue
  → Sees Pending list and Expired list
  → Can manually trigger "Sync Now"
  → Can discard individual items
  → Can clear all expired items
```
