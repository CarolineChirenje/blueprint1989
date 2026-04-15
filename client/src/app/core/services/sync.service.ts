import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, from, Observable } from 'rxjs';
import { OfflineQueueService } from './offline-queue.service';

export interface SyncResult {
  synced: number;
  failed: number;
  expired: number;
}

@Injectable({ providedIn: 'root' })
export class SyncService implements OnDestroy {
  isSyncing$ = new BehaviorSubject<boolean>(false);
  lastResult$ = new BehaviorSubject<SyncResult | null>(null);

  private swMessageHandler = (event: MessageEvent) => {
    if (event.data?.type === 'OFFLINE_SYNC_COMPLETE') {
      this.offlineQueueService.refreshCount();
      this.lastResult$.next({
        synced:  event.data.synced  ?? 0,
        failed:  event.data.failed  ?? 0,
        expired: event.data.expired ?? 0,
      });
    }
  };

  constructor(
    private http: HttpClient,
    private offlineQueueService: OfflineQueueService,
  ) {
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.addEventListener('message', this.swMessageHandler);
    }
  }

  ngOnDestroy() {
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.removeEventListener('message', this.swMessageHandler);
    }
  }

  /** Manually replay all pending queue items via HttpClient. */
  syncNow(): Observable<SyncResult> {
    return from(this._syncNow());
  }

  private async _syncNow(): Promise<SyncResult> {
    if (this.isSyncing$.value) return { synced: 0, failed: 0, expired: 0 };

    this.isSyncing$.next(true);
    const result: SyncResult = { synced: 0, failed: 0, expired: 0 };

    try {
      const items = await this.offlineQueueService.getPendingItems();
      const now   = Date.now();

      for (const item of items) {
        if (item.expiresAt < now) {
          await this.offlineQueueService.removeItem(item.id);
          result.expired++;
          continue;
        }

        try {
          await new Promise<void>((resolve, reject) => {
            const headers = new HttpHeaders({
              'Content-Type':  'application/json',
              'Authorization': `Bearer ${item.token}`,
            });
            this.http.request(item.method, item.url, { headers, body: item.body })
              .subscribe({ next: () => resolve(), error: reject });
          });
          await this.offlineQueueService.removeItem(item.id);
          result.synced++;
        } catch {
          result.failed++;
        }
      }

      this.lastResult$.next(result);
      await this.offlineQueueService.refreshCount();
    } finally {
      this.isSyncing$.next(false);
    }

    return result;
  }
}
