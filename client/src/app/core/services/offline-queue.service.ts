import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type OfflineQueueItemType = 'assessment' | 'incident' | 'meal-entry' | 'blood-pressure' | 'bp-incident';

export interface OfflineQueueItem {
  id: string;
  type: OfflineQueueItemType;
  url: string;
  method: string;
  body: Record<string, unknown>;
  token: string;
  capturedAt: string;   // ISO string
  expiresAt: number;    // epoch ms (capturedAt + 24 h)
  label: string;        // human-readable description
}

const DB_NAME = 'DivvyNotifications';
const DB_VERSION = 2;
const STORE = 'offlineQueue';
const TTL_MS = 24 * 60 * 60 * 1000; // 24 hours

/** Wrap an IDBRequest in a Promise */
function idbReq<T>(req: IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror  = () => reject(req.error);
  });
}

@Injectable({ providedIn: 'root' })
export class OfflineQueueService {
  /** Emits the current count of non-expired pending items. */
  pendingCount$ = new BehaviorSubject<number>(0);

  private dbPromise: Promise<IDBDatabase> | null = null;

  private openDB(): Promise<IDBDatabase> {
    if (this.dbPromise) return this.dbPromise;
    this.dbPromise = new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, DB_VERSION);
      req.onerror = () => reject(req.error);
      req.onsuccess = () => resolve(req.result as IDBDatabase);
      req.onupgradeneeded = (event: IDBVersionChangeEvent) => {
        const db = (event.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains('notifications')) {
          const s = db.createObjectStore('notifications', { keyPath: 'id' });
          s.createIndex('scheduledTime', 'scheduledTime', { unique: false });
          s.createIndex('sent', 'sent', { unique: false });
        }
        if (!db.objectStoreNames.contains(STORE)) {
          const qs = db.createObjectStore(STORE, { keyPath: 'id' });
          qs.createIndex('capturedAt', 'capturedAt', { unique: false });
          qs.createIndex('type', 'type', { unique: false });
          qs.createIndex('expiresAt', 'expiresAt', { unique: false });
        }
      };
    });
    return this.dbPromise;
  }

  /** Add an item to the queue and trigger Background Sync. */
  async enqueue(item: Omit<OfflineQueueItem, 'id' | 'capturedAt' | 'expiresAt'>): Promise<string> {
    const id = crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    const capturedAt = new Date().toISOString();
    const expiresAt  = Date.now() + TTL_MS;

    const full: OfflineQueueItem = { ...item, id, capturedAt, expiresAt };

    const db = await this.openDB();
    const tx = db.transaction(STORE, 'readwrite');
    await idbReq(tx.objectStore(STORE).put(full));

    // Tell service worker to enqueue (handles Background Sync registration)
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
      navigator.serviceWorker.controller.postMessage({ type: 'ENQUEUE', item: full });
    } else {
      // Fallback: register sync via ready SW
      try {
        const reg = await navigator.serviceWorker.ready;
        if ((reg as any).sync) {
          await (reg as any).sync.register('bgl-offline-queue');
        }
      } catch { /* Background Sync not supported — manual sync only */ }
    }

    await this.refreshCount();
    return id;
  }

  /** Returns all non-expired items, sorted oldest-first. */
  async getPendingItems(): Promise<OfflineQueueItem[]> {
    const db = await this.openDB();
    const tx = db.transaction(STORE, 'readonly');
    const all = await idbReq<OfflineQueueItem[]>(tx.objectStore(STORE).getAll() as IDBRequest<OfflineQueueItem[]>);
    const now = Date.now();
    return all
      .filter(i => i.expiresAt >= now)
      .sort((a, b) => a.capturedAt.localeCompare(b.capturedAt));
  }

  /** Returns all items including expired ones (needed for queue page UI). */
  async getAllItems(): Promise<OfflineQueueItem[]> {
    const db = await this.openDB();
    const tx = db.transaction(STORE, 'readonly');
    const all = await idbReq<OfflineQueueItem[]>(tx.objectStore(STORE).getAll() as IDBRequest<OfflineQueueItem[]>);
    return all.sort((a, b) => a.capturedAt.localeCompare(b.capturedAt));
  }

  /** Remove a single item by id. */
  async removeItem(id: string): Promise<void> {
    const db = await this.openDB();
    const tx = db.transaction(STORE, 'readwrite');
    await idbReq(tx.objectStore(STORE).delete(id));
    await this.refreshCount();
  }

  /** Delete all expired entries. */
  async clearExpired(): Promise<number> {
    const db = await this.openDB();
    const tx = db.transaction(STORE, 'readonly');
    const all = await idbReq<OfflineQueueItem[]>(tx.objectStore(STORE).getAll() as IDBRequest<OfflineQueueItem[]>);
    const now = Date.now();
    const expired = all.filter(i => i.expiresAt < now);
    for (const item of expired) {
      const dtx = db.transaction(STORE, 'readwrite');
      await idbReq(dtx.objectStore(STORE).delete(item.id));
    }
    await this.refreshCount();
    return expired.length;
  }

  /** Refresh and emit the current pending count. */
  async refreshCount(): Promise<void> {
    const db = await this.openDB();
    const tx = db.transaction(STORE, 'readonly');
    const all = await idbReq<OfflineQueueItem[]>(tx.objectStore(STORE).getAll() as IDBRequest<OfflineQueueItem[]>);
    const now = Date.now();
    this.pendingCount$.next(all.filter(i => i.expiresAt >= now).length);
  }
}
