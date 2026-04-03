import { Injectable, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { ServiceWorkerNotificationService } from './sw-notification.service';

export interface PushSubscribeRequest {
  endpoint: string;
  p256dh: string;
  auth: string;
  userAgent?: string;
}

export interface TestPushRequest {
  type: number;
  title: string;
  body: string;
  deepLinkUrl?: string;
}

/** Enum mirroring server-side NotificationType — kept in sync with the server enum values. */
export enum NotificationType {
  General              = 1,
  PaymentDue           = 2,
  PaymentReceived      = 3,
  CycleCreated         = 4,
  SystemRestart        = 5,
  GroupInviteReceived  = 6,
}

@Injectable({ providedIn: 'root' })
export class PushNotificationService implements OnDestroy {

  private readonly apiUrl = `${environment.apiUrl}/push`;
  private readonly destroy$ = new Subject<void>();

  /** Current browser notification permission. */
  private _permission = new BehaviorSubject<NotificationPermission>(
    'Notification' in window ? Notification.permission : 'denied'
  );
  readonly permission$ = this._permission.asObservable();

  /** Whether this browser is currently subscribed to server push. */
  private _isSubscribed = new BehaviorSubject<boolean>(false);
  readonly isSubscribed$ = this._isSubscribed.asObservable();

  constructor(
    private http: HttpClient,
    private swService: ServiceWorkerNotificationService
  ) {
    this.init();
  }

  // ── Initialisation ─────────────────────────────────────────────────────────

  private async init() {
    const registration = await this.swService.getRegistration();
    if (!registration) return;

    // Check if already subscribed
    const existing = await registration.pushManager.getSubscription();
    this._isSubscribed.next(!!existing);

    // Listen for the SW's re-subscription request (browser rotated keys)
    navigator.serviceWorker.addEventListener('message', (event) => {
      if (event.data?.type === 'PUSH_SUBSCRIPTION_CHANGED') {
        this.subscribeToServer().catch(err =>
          console.error('PushNotificationService: re-subscribe failed', err)
        );
      }
    });
  }

  // ── Public API ──────────────────────────────────────────────────────────────

  /**
   * Requests notification permission from the browser.
   * Must be called from a user gesture.
   */
  async requestPermission(): Promise<NotificationPermission> {
    if (!('Notification' in window)) {
      this._permission.next('denied');
      return 'denied';
    }

    const result = await Notification.requestPermission();
    this._permission.next(result);
    return result;
  }

  /**
   * Subscribes the current browser to server push via PushManager,
   * fetching the VAPID public key from the server at runtime.
   * Automatically registers the subscription with the API.
   */
  async subscribeToServer(): Promise<boolean> {
    try {
      const registration = await this.swService.getRegistration();
      if (!registration) {
        console.warn('PushNotificationService: No SW registration available.');
        return false;
      }

      // Check / request permission
      if (Notification.permission !== 'granted') {
        const perm = await this.requestPermission();
        if (perm !== 'granted') {
          console.warn('PushNotificationService: Notification permission denied.');
          return false;
        }
      }

      // Fetch VAPID public key from server (runtime — no bake-in needed)
      const { publicKey } = await this.http
        .get<{ publicKey: string }>(`${this.apiUrl}/vapid-public-key`)
        .toPromise() as { publicKey: string };

      const applicationServerKey = this.urlB64ToUint8Array(publicKey);

      // Subscribe via PushManager
      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: applicationServerKey.buffer as ArrayBuffer
      });

      // Register with server
      await this.sendSubscriptionToServer(subscription);
      this._isSubscribed.next(true);
      return true;
    } catch (err) {
      console.error('PushNotificationService: subscribe failed', err);
      return false;
    }
  }

  /** Unsubscribes from push and removes the subscription from the server. */
  async unsubscribeFromServer(): Promise<void> {
    try {
      const registration = await this.swService.getRegistration();
      if (!registration) return;

      const subscription = await registration.pushManager.getSubscription();
      if (!subscription) {
        this._isSubscribed.next(false);
        return;
      }

      // Notify server first
      await this.http.delete(`${this.apiUrl}/unsubscribe`, {
        body: this.buildSubscribeRequest(subscription)
      }).toPromise();

      await subscription.unsubscribe();
      this._isSubscribed.next(false);
    } catch (err) {
      console.error('PushNotificationService: unsubscribe failed', err);
    }
  }

  /**
   * Sends a test push to the authenticated user.
   * Available in Development for any user; in Production only for Admins.
   */
  sendTestPush(req: TestPushRequest) {
    return this.http.post<{ message: string }>(`${this.apiUrl}/test`, req);
  }

  /**
   * Schedules a server-side BGL recheck reminder.
   * @param careRecipientId The ID of the care recipient for the reminder.
   * @param delayMinutes Delay before the push is sent (default 120 = 2 hours).
   */
  scheduleBgTimer(careRecipientId: number, delayMinutes = 120) {
    return this.http.post<{ reminderId: number; scheduledAt: string; delayMinutes: number }>(
      `${this.apiUrl}/bg-timer/schedule`,
      { careRecipientId, delayMinutes }
    );
  }

  /** Cancels a pending BG timer reminder for the specified care recipient. */
  cancelBgTimer(careRecipientId: number) {
    return this.http.delete(`${this.apiUrl}/bg-timer/cancel`, {
      params: { careRecipientId: careRecipientId.toString() }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  private async sendSubscriptionToServer(sub: PushSubscription): Promise<void> {
    const req = this.buildSubscribeRequest(sub);
    await this.http.post(`${this.apiUrl}/subscribe`, req).toPromise();
  }

  private buildSubscribeRequest(sub: PushSubscription): PushSubscribeRequest {
    const json = sub.toJSON();
    return {
      endpoint: sub.endpoint,
      p256dh: json.keys?.['p256dh'] ?? '',
      auth: json.keys?.['auth'] ?? '',
      userAgent: navigator.userAgent.substring(0, 500)
    };
  }

  /**
   * Converts a base64url VAPID public key to a Uint8Array
   * as required by PushManager.subscribe({ applicationServerKey }).
   */
  private urlB64ToUint8Array(base64String: string): Uint8Array {
    const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    return Uint8Array.from([...rawData].map(char => char.charCodeAt(0)));
  }
}
