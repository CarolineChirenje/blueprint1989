// Hybrid Service Worker — Batanai
// Delegates caching/fetch to the Angular ngsw-worker.js runtime.
// This file owns: push notifications, scheduled local notifications,
//                 IndexedDB storage and notification-click routing.
importScripts('/ngsw-worker.js');

// Custom Service Worker for Batanai
// Handles:
//   1. Scheduled local notifications (ketone recheck timer)
//   2. Server-sent push notifications (VAPID-signed)

const CACHE_NAME = 'batanai-v2';
const NOTIFICATION_CHECK_INTERVAL = 60000; // Check every minute

// Default app icon
const ICON = '/assets/icons/icon-192x192.png';

// Install event
self.addEventListener('install', (event) => {
  console.log('Service Worker: Installing...');
  self.skipWaiting();
});

// Activate event
self.addEventListener('activate', (event) => {
  console.log('Service Worker: Activating...');
  event.waitUntil(
    caches.keys()
      .then(keys => Promise.all(
        // Delete every cache that isn't the current version.
        // This includes old ngsw caches so fresh styles/assets are fetched.
        keys
          .filter(key => key !== CACHE_NAME)
          .map(key => {
            console.log('Service Worker: Deleting stale cache', key);
            return caches.delete(key);
          })
      ))
      .then(() => self.clients.claim())
      .then(() => {
        // Reload all open tabs so they immediately get fresh content
        return self.clients.matchAll({ type: 'window' });
      })
      .then(clients => {
        clients.forEach(client => client.navigate(client.url));
      })
  );

  // Start checking for scheduled notifications
  startNotificationChecker();
});

// Message event - listen for commands from the app
self.addEventListener('message', (event) => {
  console.log('Service Worker: Received message', event.data);
  
  if (event.data.type === 'SCHEDULE_NOTIFICATION') {
    scheduleNotification(event.data.payload);
  } else if (event.data.type === 'CANCEL_NOTIFICATION') {
    cancelNotification(event.data.notificationId);
  } else if (event.data.type === 'CHECK_SCHEDULED') {
    checkScheduledNotifications();
  } else if (event.data.type === 'ENQUEUE') {
    event.waitUntil(enqueueOfflineItem(event.data.item));
  } else if (event.data.type === 'GET_QUEUE_COUNT') {
    event.waitUntil(
      getOfflineQueueCount().then(count => {
        event.source && event.source.postMessage({ type: 'QUEUE_COUNT', count });
      })
    );
  }
});

// ─── Background Sync ────────────────────────────────────────────────────────

self.addEventListener('sync', (event) => {
  if (event.tag === 'bgl-offline-queue') {
    event.waitUntil(replayOfflineQueue());
  }
});

async function enqueueOfflineItem(item) {
  try {
    const db = await openNotificationDB();
    const tx = db.transaction('offlineQueue', 'readwrite');
    const store = tx.objectStore('offlineQueue');
    await idbReq(store.put(item));
    console.log('Service Worker: Offline item enqueued', item.id);

    // Register background sync if supported
    if (self.registration.sync) {
      await self.registration.sync.register('bgl-offline-queue');
    }
  } catch (err) {
    console.error('Service Worker: Failed to enqueue item', err);
  }
}

async function replayOfflineQueue() {
  const db = await openNotificationDB();
  const tx = db.transaction('offlineQueue', 'readonly');
  const store = tx.objectStore('offlineQueue');
  const items = await idbReq(store.getAll());

  let synced = 0;
  let failed = 0;
  let expired = 0;
  const now = Date.now();

  for (const item of items) {
    // Purge expired entries
    if (item.expiresAt < now) {
      expired++;
      const delTx = db.transaction('offlineQueue', 'readwrite');
      await idbReq(delTx.objectStore('offlineQueue').delete(item.id));
      continue;
    }

    try {
      const response = await fetch(item.url, {
        method: item.method,
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${item.token}`
        },
        body: JSON.stringify(item.body)
      });

      if (response.ok) {
        const delTx = db.transaction('offlineQueue', 'readwrite');
        await idbReq(delTx.objectStore('offlineQueue').delete(item.id));
        synced++;
      } else {
        failed++;
      }
    } catch {
      failed++;
    }
  }

  // Notify all open clients
  const clients = await self.clients.matchAll();
  clients.forEach(client => {
    client.postMessage({ type: 'OFFLINE_SYNC_COMPLETE', synced, failed, expired });
  });
}

async function getOfflineQueueCount() {
  try {
    const db = await openNotificationDB();
    const tx = db.transaction('offlineQueue', 'readonly');
    const store = tx.objectStore('offlineQueue');
    const items = await idbReq(store.getAll());
    const now = Date.now();
    return items.filter(i => i.expiresAt >= now).length;
  } catch {
    return 0;
  }
}

// ─── Server-sent push notification ─────────────────────────────────────────

// Push event: triggered by the server via VAPID-signed Web Push
self.addEventListener('push', (event) => {
  console.log('Service Worker: Push received', event);

  let payload = {
    title: 'Batanai Notification',
    body: 'You have a new notification.',
    icon: ICON,
    badge: ICON,
    url: '/',
    type: 0
  };

  if (event.data) {
    try {
      payload = { ...payload, ...event.data.json() };
    } catch (e) {
      payload.body = event.data.text();
    }
  }

  const notificationOptions = {
    body: payload.body,
    icon: payload.icon || ICON,
    badge: payload.badge || ICON,
    tag: `push-${payload.type}-${Date.now()}`,
    requireInteraction: isPriorityType(payload.type),
    data: { url: payload.url, source: 'push', type: payload.type },
    actions: [
      { action: 'open', title: 'View' },
      { action: 'dismiss', title: 'Dismiss' }
    ]
  };

  event.waitUntil(
    self.registration.showNotification(payload.title, notificationOptions)
  );
});

// Re-subscribe when the push subscription changes (e.g., browser rotated keys)
self.addEventListener('pushsubscriptionchange', (event) => {
  console.log('Service Worker: Push subscription changed');
  event.waitUntil(
    self.clients.matchAll().then(clients =>
      clients.forEach(client =>
        client.postMessage({ type: 'PUSH_SUBSCRIPTION_CHANGED' })
      )
    )
  );
});

// Returns true for alert types that should persist until dismissed
function isPriorityType(type) {
  // IncidentSeverity=2, AssessmentSeverity=3, BgTimerReminder=6
  return [2, 3, 6].includes(type);
}

// Schedule a notification
async function scheduleNotification(payload) {
  const { id, title, body, scheduledTime, data } = payload;
  
  try {
    // Store in IndexedDB
    const db = await openNotificationDB();
    const tx = db.transaction('notifications', 'readwrite');
    const store = tx.objectStore('notifications');
    
    await idbReq(store.put({
      id,
      title,
      body,
      scheduledTime,
      data,
      sent: false
    }));
    
    console.log('Service Worker: Notification scheduled for', new Date(scheduledTime));
  } catch (error) {
    console.error('Service Worker: Error scheduling notification', error);
  }
}

// Cancel a scheduled notification
async function cancelNotification(notificationId) {
  try {
    const db = await openNotificationDB();
    const tx = db.transaction('notifications', 'readwrite');
    const store = tx.objectStore('notifications');
    await idbReq(store.delete(notificationId));
    
    console.log('Service Worker: Notification cancelled', notificationId);
  } catch (error) {
    console.error('Service Worker: Error cancelling notification', error);
  }
}

// Check for notifications that need to be sent
async function checkScheduledNotifications() {
  try {
    const db = await openNotificationDB();
    const readTx = db.transaction('notifications', 'readonly');
    const notifications = await idbReq(readTx.objectStore('notifications').getAll());
    
    const now = Date.now();
    
    for (const notification of notifications) {
      if (!notification.sent && notification.scheduledTime <= now) {
        // Send the notification
        await self.registration.showNotification(notification.title, {
          body: notification.body,
          icon: '/assets/icons/icon-192x192.png',
          badge: '/assets/icons/icon-192x192.png',
          tag: notification.id,
          requireInteraction: true,
          data: notification.data,
          actions: [
            {
              action: 'open',
              title: 'Open App'
            },
            {
              action: 'dismiss',
              title: 'Dismiss'
            }
          ]
        });
        
        // Mark as sent in a fresh transaction because the original read transaction
        // will be inactive after the awaited notification/display work above.
        const writeTx = db.transaction('notifications', 'readwrite');
        const updatedNotification = { ...notification, sent: true };
        await idbReq(writeTx.objectStore('notifications').put(updatedNotification));
        
        console.log('Service Worker: Notification sent', notification.id);
        
        // Notify all clients that notification was sent
        const clients = await self.clients.matchAll();
        clients.forEach(client => {
          client.postMessage({
            type: 'NOTIFICATION_SENT',
            notificationId: notification.id
          });
        });
      }
    }
  } catch (error) {
    console.error('Service Worker: Error checking notifications', error);
  }
}

// Notification click event
self.addEventListener('notificationclick', (event) => {
  console.log('Service Worker: Notification clicked', event.notification.tag);
  
  event.notification.close();
  
  if (event.action === 'dismiss') return;

  // Determine where to navigate:
  // - Server push notifications carry a deep-link URL in notification.data.url
  // - Local timer notifications fall back to the BGL reading page
  const notifData = event.notification.data || {};
  const deepLink  = notifData.source === 'push' && notifData.url
    ? notifData.url
    : '/admin/bgl-reading';

  event.waitUntil(
    self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((clientList) => {
      // Try to focus an existing window on the target path
      for (const client of clientList) {
        if (client.url.includes(deepLink) && 'focus' in client) {
          return client.focus();
        }
      }
      // Focus any open window and navigate it, or open a new one
      if (clientList.length > 0 && 'navigate' in clientList[0]) {
        return clientList[0].focus().then(c => c.navigate(deepLink));
      }
      if (self.clients.openWindow) {
        return self.clients.openWindow(deepLink);
      }
    })
  );
});

// Start periodic notification checker
function startNotificationChecker() {
  setInterval(() => {
    checkScheduledNotifications();
  }, NOTIFICATION_CHECK_INTERVAL);
  
  // Also check immediately
  checkScheduledNotifications();
}

// Wrap an IDBRequest in a Promise so it can be awaited.
// Raw IDBRequest objects are NOT Promises — await-ing them without this
// resolves to the request object itself, not the result.
function idbReq(request) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror  = () => reject(request.error);
  });
}

// Open IndexedDB (v2 adds offlineQueue store alongside notifications)
function openNotificationDB() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open('BatanaiNotifications', 2);
    
    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
    
    request.onupgradeneeded = (event) => {
      const db = event.target.result;
      
      if (!db.objectStoreNames.contains('notifications')) {
        const store = db.createObjectStore('notifications', { keyPath: 'id' });
        store.createIndex('scheduledTime', 'scheduledTime', { unique: false });
        store.createIndex('sent', 'sent', { unique: false });
      }

      if (!db.objectStoreNames.contains('offlineQueue')) {
        const qs = db.createObjectStore('offlineQueue', { keyPath: 'id' });
        qs.createIndex('capturedAt', 'capturedAt', { unique: false });
        qs.createIndex('type', 'type', { unique: false });
        qs.createIndex('expiresAt', 'expiresAt', { unique: false });
      }
    };
  });
}

// Fetch is owned by ngsw-worker.js (imported above).
// No custom fetch handler here — adding a second respondWith() would throw.
