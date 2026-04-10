import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import {
  InstallPromptStatus,
  RegisterDeviceRequest,
  RegisterDeviceResponse,
  UpdateInstallStatusRequest,
  UserDeviceDto
} from '../../shared/models/device.model';

const CLIENT_ID_KEY = 'bgl_client_id';
const INSTALL_STATUS_KEY = 'bgl_install_status';
const NEXT_PROMPT_KEY = 'bgl_next_prompt_at';
const SAMSUNG_WARN_DISMISSED_KEY = 'bgl_samsung_warn_dismissed';
const SAMSUNG_REINSTALL_DISMISSED_KEY = 'bgl_samsung_reinstall_dismissed';

@Injectable({ providedIn: 'root' })
export class DeviceService {
  private readonly apiUrl = `${environment.apiUrl}/me/devices`;

  /** True for mobile/tablet — installs are only relevant on those. */
  static readonly isMobileOrTablet: boolean =
    /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent) ||
    navigator.maxTouchPoints > 0;

  /** True when running inside Samsung Internet browser. */
  static readonly isSamsungInternet: boolean =
    /SamsungBrowser/i.test(navigator.userAgent);

  /** True when the app is running as an installed standalone PWA. */
  static readonly isRunningStandalone: boolean =
    window.matchMedia('(display-mode: standalone)').matches ||
    (window.navigator as any).standalone === true;

  private _installStatus$ = new BehaviorSubject<InstallPromptStatus>(
    (localStorage.getItem(INSTALL_STATUS_KEY) as InstallPromptStatus) ?? InstallPromptStatus.Unknown
  );
  readonly installStatus$ = this._installStatus$.asObservable();

  private _nextPromptAt$ = new BehaviorSubject<Date | null>(
    localStorage.getItem(NEXT_PROMPT_KEY) ? new Date(localStorage.getItem(NEXT_PROMPT_KEY)!) : null
  );
  readonly nextPromptAt$ = this._nextPromptAt$.asObservable();

  private static parseDate(dateStr: string | null): Date | null {
    if (!dateStr) return null;
    // Server omits 'Z' with legacy Npgsql — force UTC interpretation
    const utc = dateStr.endsWith('Z') || /[+\-]\d{2}:\d{2}$/.test(dateStr)
      ? dateStr : dateStr + 'Z';
    return new Date(utc);
  }

  constructor(private http: HttpClient, private auth: AuthService) {}

  // ── Helpers ───────────────────────────────────────────────────────────────

  /** Returns or creates a stable UUID for this browser. */
  getClientId(): string {
    let id = localStorage.getItem(CLIENT_ID_KEY);
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem(CLIENT_ID_KEY, id);
    }
    return id;
  }

  get currentInstallStatus(): InstallPromptStatus {
    return this._installStatus$.value;
  }

  get nextPromptAt(): Date | null {
    return this._nextPromptAt$.value;
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${this.auth.getToken()}` });
  }

  private updateLocalCache(status: InstallPromptStatus, nextPromptAt: Date | null): void {
    localStorage.setItem(INSTALL_STATUS_KEY, status);
    this._installStatus$.next(status);
    if (nextPromptAt) {
      localStorage.setItem(NEXT_PROMPT_KEY, nextPromptAt.toISOString());
    } else {
      localStorage.removeItem(NEXT_PROMPT_KEY);
    }
    this._nextPromptAt$.next(nextPromptAt);
  }

  // ── API calls ─────────────────────────────────────────────────────────────

  /**
   * Called once on login / app load. Registers this device (or refreshes LastSeenAt)
   * and gets back the persistent install status from the server.
   */
  register(): Observable<RegisterDeviceResponse> {
    const request: RegisterDeviceRequest = {
      clientId: this.getClientId(),
      userAgent: navigator.userAgent,
      appVersion: environment.version
    };

    return this.http
      .post<RegisterDeviceResponse>(`${this.apiUrl}/register`, request, { headers: this.headers() })
      .pipe(
        tap(response => {
          this.updateLocalCache(
            response.installStatus,
            DeviceService.parseDate(response.nextPromptAt)
          );
        })
      );
  }

  /** Tell the server the user clicked "Install" — status = Installed (4). */
  markInstalled(): Observable<void> {
    return this.updateInstallStatus(4, null);
  }

  /** Tell the server the user dismissed "for now" — status = RemindLater (1).  */
  markRemindLater(remindAt: Date): Observable<void> {
    return this.updateInstallStatus(1, remindAt);
  }

  /** Tell the server the user clicked "Never ask again" — status = NeverAskAgain (3). */
  markNeverAskAgain(): Observable<void> {
    return this.updateInstallStatus(3, null);
  }

  /** Reset install prompt to Unknown — will show the banner again next time. */
  resetInstallPrompt(): Observable<void> {
    return this.updateInstallStatus(0, null);
  }

  // ── Samsung Internet warning banners ─────────────────────────────────────

  get samsungWarnBannerDismissed(): boolean {
    return localStorage.getItem(SAMSUNG_WARN_DISMISSED_KEY) !== null;
  }

  get samsungReinstallBannerDismissed(): boolean {
    return localStorage.getItem(SAMSUNG_REINSTALL_DISMISSED_KEY) !== null;
  }

  dismissSamsungWarnBanner(): void {
    localStorage.setItem(SAMSUNG_WARN_DISMISSED_KEY, '1');
  }

  dismissSamsungReinstallBanner(): void {
    localStorage.setItem(SAMSUNG_REINSTALL_DISMISSED_KEY, '1');
  }

  private updateInstallStatus(status: number, nextPromptAt: Date | null): Observable<void> {
    const request: UpdateInstallStatusRequest = {
      clientId: this.getClientId(),
      installStatus: status,
      nextPromptAt: nextPromptAt?.toISOString() ?? null
    };

    const enumStatus = this.numericToEnum(status);
    this.updateLocalCache(enumStatus, nextPromptAt); // optimistic update

    return this.http.patch<void>(`${this.apiUrl}/install-status`, request, { headers: this.headers() });
  }

  /** List all registered devices for the current user. */
  getDevices(): Observable<UserDeviceDto[]> {
    return this.http.get<UserDeviceDto[]>(this.apiUrl, { headers: this.headers() });
  }

  /** Deactivate (soft-delete) a device. */
  deleteDevice(deviceId: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${deviceId}`, { headers: this.headers() });
  }

  /** Rename a device with a user-supplied friendly label. */
  renameDevice(deviceId: number, friendlyName: string): Observable<void> {
    return this.http.patch<void>(
      `${this.apiUrl}/${deviceId}/rename`,
      { friendlyName },
      { headers: this.headers() }
    );
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  private numericToEnum(n: number): InstallPromptStatus {
    switch (n) {
      case 1: return InstallPromptStatus.RemindLater;
      case 2: return InstallPromptStatus.Deferred;
      case 3: return InstallPromptStatus.NeverAskAgain;
      case 4: return InstallPromptStatus.Installed;
      default: return InstallPromptStatus.Unknown;
    }
  }
}
