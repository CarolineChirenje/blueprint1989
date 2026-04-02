import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ScheduledNotification {
  id: string;
  title: string;
  body: string;
  scheduledTime: number;
  data?: any;
}

@Injectable({
  providedIn: 'root'
})
export class ServiceWorkerNotificationService {
  readonly HYPO_KEY = 'bgl-hypo-timer';
  readonly KETONE_KEY = 'bgl-ketone-timer';

  private swRegistration: ServiceWorkerRegistration | null = null;
  // Observable-friendly promise resolved after registration (used by PushNotificationService)
  private _registrationPromise: Promise<ServiceWorkerRegistration | null>;
  private _resolveRegistration!: (reg: ServiceWorkerRegistration | null) => void;

  constructor() {
    this._registrationPromise = new Promise(resolve => {
      this._resolveRegistration = resolve;
    });
    this.registerServiceWorker();
  }

  /** Returns the SW registration (resolves after the SW registers, or null if unsupported). */
  getRegistration(): Promise<ServiceWorkerRegistration | null> {
    return this._registrationPromise;
  }

  // Register custom service worker — enabled in BOTH dev and prod
  async registerServiceWorker() {
    if (!('serviceWorker' in navigator)) {
      console.warn('Service Worker not supported in this browser.');
      this._resolveRegistration(null);
      return;
    }

    try {
      const registration = await navigator.serviceWorker.register('/custom-sw.js', {
        scope: '/'
      });

      this.swRegistration = registration;
      this._resolveRegistration(registration);

      // Listen for messages from service worker
      navigator.serviceWorker.addEventListener('message', (event) => {
        if (event.data.type === 'NOTIFICATION_SENT') {
          // Clear timer state from localStorage — try both keys
          this.clearTimerState(event.data.notificationId, this.HYPO_KEY);
          this.clearTimerState(event.data.notificationId, this.KETONE_KEY);
        }
      });

      // Request notification permission
      await this.requestNotificationPermission();

    } catch (error) {
      console.error('Service Worker registration failed:', error);
      this._resolveRegistration(null);
    }
  }

  // Request notification permission
  async requestNotificationPermission(): Promise<NotificationPermission> {
    if ('Notification' in window) {
      const permission = await Notification.requestPermission();
      return permission;
    }
    return 'denied';
  }

  // Schedule a notification through the service worker
  async scheduleNotification(notification: ScheduledNotification, key?: string) {
    if (!this.swRegistration || !this.swRegistration.active) {
      console.error('Service Worker not active');
      return;
    }

    // Send message to service worker
    this.swRegistration.active.postMessage({
      type: 'SCHEDULE_NOTIFICATION',
      payload: notification
    });

    // Also store in localStorage as backup
    this.saveTimerState(notification, key);
  }

  // Cancel a scheduled notification
  async cancelNotification(notificationId: string) {
    if (!this.swRegistration || !this.swRegistration.active) {
      return;
    }

    this.swRegistration.active.postMessage({
      type: 'CANCEL_NOTIFICATION',
      notificationId
    });

    // Remove from localStorage
    this.clearTimerState(notificationId);
  }

  // Save timer state to localStorage
  private saveTimerState(notification: ScheduledNotification, key?: string) {
    try {
      const timerState = {
        notificationId: notification.id,
        scheduledTime: notification.scheduledTime,
        startTime: Date.now(),
        data: notification.data
      };
      localStorage.setItem(key ?? this.KETONE_KEY, JSON.stringify(timerState));
    } catch (error) {
      console.error('Error saving timer state:', error);
    }
  }

  // Get timer state from localStorage
  getTimerState(key?: string): any {
    try {
      const state = localStorage.getItem(key ?? this.KETONE_KEY);
      return state ? JSON.parse(state) : null;
    } catch (error) {
      console.error('Error getting timer state:', error);
      return null;
    }
  }

  // Clear timer state from localStorage
  clearTimerState(notificationId?: string, key?: string) {
    try {
      const storageKey = key ?? this.KETONE_KEY;
      const currentState = this.getTimerState(storageKey);
      if (!notificationId || (currentState && currentState.notificationId === notificationId)) {
        localStorage.removeItem(storageKey);
      }
    } catch (error) {
      console.error('Error clearing timer state:', error);
    }
  }

  // Check if there's an active timer
  hasActiveTimer(key?: string): boolean {
    const state = this.getTimerState(key);
    if (!state) return false;
    
    const now = Date.now();
    return state.scheduledTime > now;
  }

  // Get remaining time for active timer
  getRemainingTime(key?: string): number {
    const state = this.getTimerState(key);
    if (!state) return 0;
    
    const now = Date.now();
    const remaining = Math.max(0, Math.floor((state.scheduledTime - now) / 1000));
    return remaining;
  }
}
