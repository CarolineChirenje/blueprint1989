import { ChangeDetectorRef, Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { OfflineQueueService, OfflineQueueItem } from '../../../core/services/offline-queue.service';
import { SyncService, SyncResult } from '../../../core/services/sync.service';

@Component({
  selector: 'app-offline-queue',
  templateUrl: './offline-queue.component.html',
  styleUrls: ['./offline-queue.component.css'],
  standalone: false
})
export class OfflineQueueComponent implements OnInit, OnDestroy {
  items: OfflineQueueItem[] = [];
  isLoading = true;
  isSyncing = false;
  syncResult: SyncResult | null = null;
  syncError = '';

  private subs = new Subscription();

  constructor(
    private offlineQueue: OfflineQueueService,
    private syncService: SyncService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.subs.add(
      this.syncService.isSyncing$.subscribe(v => { this.isSyncing = v; this.cdr.detectChanges(); })
    );
    this.subs.add(
      this.syncService.lastResult$.subscribe(r => { this.syncResult = r; this.cdr.detectChanges(); })
    );
    this.loadItems();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  async loadItems(): Promise<void> {
    this.isLoading = true;
    this.items = await this.offlineQueue.getAllItems();
    this.isLoading = false;
  }

  get pendingItems(): OfflineQueueItem[] {
    const now = Date.now();
    return this.items.filter(i => i.expiresAt >= now);
  }

  get expiredItems(): OfflineQueueItem[] {
    const now = Date.now();
    return this.items.filter(i => i.expiresAt < now);
  }

  get isOnline(): boolean {
    return navigator.onLine;
  }

  get pendingCount(): number {
    return this.pendingItems.length;
  }

  typeLabel(type: string): string {
    switch (type) {
      default:            return type;
    }
  }

  typeBadgeClass(type: string): string {
    switch (type) {
      default:           return 'badge-default';
    }
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleString();
  }

  expiryLabel(item: OfflineQueueItem): string {
    const remaining = item.expiresAt - Date.now();
    if (remaining <= 0) return 'Expired';
    const h = Math.floor(remaining / 3_600_000);
    const m = Math.floor((remaining % 3_600_000) / 60_000);
    return h > 0 ? `${h}h ${m}m left` : `${m}m left`;
  }

  isExpiringSoon(item: OfflineQueueItem): boolean {
    return item.expiresAt - Date.now() < 2 * 60 * 60 * 1000; // < 2 hours
  }

  isExpired(item: OfflineQueueItem): boolean {
    return item.expiresAt < Date.now();
  }

  syncNow(): void {
    this.syncError = '';
    this.syncResult = null;
    this.syncService.syncNow().subscribe({
      next: async (result) => {
        await this.loadItems();
        if (result.failed > 0) {
          this.syncError = `${result.failed} entr${result.failed === 1 ? 'y' : 'ies'} could not be uploaded — please try again.`;
        }
        this.cdr.detectChanges();
      },
      error: () => {
        this.syncError = 'Sync failed. Please check your connection and try again.';
        this.cdr.detectChanges();
      }
    });
  }

  async discardItem(id: string): Promise<void> {
    await this.offlineQueue.removeItem(id);
    await this.loadItems();
  }

  async clearExpired(): Promise<void> {
    await this.offlineQueue.clearExpired();
    await this.loadItems();
  }
}
