import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class WakeLockService {
  private sentinel: WakeLockSentinel | null = null;

  /**
   * Acquire a screen wake lock.  Silently no-ops on browsers that do not
   * support the Wake Lock API (Firefox, older Safari, desktop Chrome < 84).
   */
  async acquire(): Promise<void> {
    try {
      if (!('wakeLock' in navigator)) return;
      this.sentinel = await (navigator as any).wakeLock.request('screen');
    } catch {
      // Permission denied or API unavailable — not a user-visible error
    }
  }

  /**
   * Release the active wake lock.
   */
  async release(): Promise<void> {
    try {
      if (this.sentinel) {
        await this.sentinel.release();
        this.sentinel = null;
      }
    } catch {
      // Silently ignore — already released or never acquired
    }
  }

  /**
   * Re-acquire a wake lock after returning to the foreground.
   * The OS automatically releases the sentinel when the tab goes to the
   * background, so this must be called from a visibilitychange handler.
   */
  async reacquire(): Promise<void> {
    if (!this.sentinel) {
      await this.acquire();
    }
  }
}
