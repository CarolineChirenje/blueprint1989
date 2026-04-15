import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { Router, NavigationEnd, NavigationStart, NavigationCancel, NavigationError } from '@angular/router';
import { trigger, transition, style, animate } from '@angular/animations';
import { HttpClient } from '@angular/common/http';
import { AuthService } from './core/services/auth.service';
import { NotificationDto, NotificationService } from './core/services/notification.service';
import { NotificationType, PushNotificationService } from './core/services/push-notification.service';
import { DeviceService } from './core/services/device.service';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { Role } from './shared/models/user.model';
import { InstallPromptStatus } from './shared/models/device.model';
import { environment } from '../environments/environment';
import { filter } from 'rxjs/operators';
import { Subscription, interval } from 'rxjs';
import { OfflineQueueService } from './core/services/offline-queue.service';
import { SyncService } from './core/services/sync.service';
import { SystemService } from './shared/services/system.service';
import { DialogService } from './shared/services/dialog.service';
import { GroupService } from './core/services/group.service';
import { TourService } from './core/services/tour.service';

type BellNotificationItem = {
  ids: number[];
  message: string;
  createdAt: string;
  type: NotificationType;
  deepLinkUrl: string | null;
  count: number;
};

type NotificationGroup = {
  label: string;
  items: BellNotificationItem[];
};

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.css'],
    standalone: false,
    animations: [
      trigger('fadeRoute', [
        transition('* <=> *', [
          style({ opacity: 0, pointerEvents: 'none' }),
          animate('150ms ease-in', style({ opacity: 1, pointerEvents: 'auto' }))
        ])
      ])
    ]
})

export class AppComponent implements OnInit, OnDestroy {
  title = 'Batanai';
  version = '';
  dropdownOpen = false;
  bglDropdownOpen = false;
  managementDropdownOpen = false;
  managementSubGroupOpen: string | null = null;
  helpDropdownOpen = false;
  systemDropdownOpen = false;
  notificationDropdownOpen = false;
  diabetesDropdownOpen = false;
  vitalsDropdownOpen = false;
  mobileNavOpen = false;
  isAuthPage = false;

  readonly notificationBellLimit = 10;

  unreadCount = 0;
  notifications: NotificationDto[] = [];
  notificationGroups: NotificationGroup[] = [];

  isOffline = false;
  navProgressVisible = false;
  routeCounter = 0;
  private offlineResetTimer: any = null;

  // PWA install banner state
  showInstallBanner = false;
  pendingCount = 0;
  private installPromptEvent: any = null;

  // Samsung Internet warning banner state
  showSamsungWarnBanner = false;      // in Samsung Internet browser, not yet installed
  showSamsungReinstallBanner = false; // installed as a PWA via Samsung Internet

  // App update prompt state
  showUpdatePrompt = false;
  isBreakingUpdate = false;
  year = new Date().getFullYear();
  canManageGroups = false;

  private notifSub?: Subscription;
  private pollSub?: Subscription;

  constructor(
    private auth: AuthService,
    public router: Router,
    public notificationService: NotificationService,
    private pushService: PushNotificationService,
    private swUpdate: SwUpdate,
    public deviceService: DeviceService,
    private offlineQueueService: OfflineQueueService,
    private syncService: SyncService,
    private http: HttpClient,
    private systemService: SystemService,
    private dialogService: DialogService,
    private groupService: GroupService,
    private tourService: TourService
  ) {}
  
  ngOnInit() {
    this.hideSplash();
    this.checkAuthPage();
    this.loadVersion();

    // Update prompt: when a new build is detected, fetch version manifest
    // to decide if it's a breaking update (forced) or standard (dismissible).
    if (this.swUpdate.isEnabled) {
      this.swUpdate.versionUpdates.pipe(
        filter((evt): evt is VersionReadyEvent => evt.type === 'VERSION_READY')
      ).subscribe(async () => {
        const manifest = await this.fetchVersionManifest();
        this.isBreakingUpdate = manifest
          ? (manifest.breaking === true || this.isVersionOlder(environment.version, manifest.minRequired))
          : false;
        if (!this.isAuthPage) this.showUpdatePrompt = true;
      });
      // Trigger an immediate check on startup (SW normally checks every 6 hours)
      this.swUpdate.checkForUpdate();
    }

    // Staleness check: if the running version is older than minRequired,
    // force the update banner immediately (catches long-absent users).
    this.checkStalenessOnStartup();


    this.notifSub = this.notificationService.unreadCount$.subscribe(count => {
      this.unreadCount = count;
    });

    this.notifSub.add(this.notificationService.notifications$.subscribe(n => {
      this.notifications = n;
      this.notificationGroups = this.groupNotifications(n);
    }));

    // Poll every 60 seconds when logged in
    this.pollSub = interval(60_000).subscribe(() => {
      if (this.isLoggedIn() && !this.isAuthPage) {
        this.loadNotifications();
      }
    });

    // Offline queue: purge expired, load pending count
    this.offlineQueueService.clearExpired().then(() => this.offlineQueueService.refreshCount());
    this.notifSub!.add(
      this.offlineQueueService.pendingCount$.subscribe(n => this.pendingCount = n)
    );

    // Listen for SW sync-complete messages
    if ('serviceWorker' in navigator) {
      navigator.serviceWorker.addEventListener('message', (e: MessageEvent) => {
        if (e.data?.type === 'OFFLINE_SYNC_COMPLETE') {
          this.offlineQueueService.refreshCount();
        }
      });
    }

    // If user is already logged in on app load, initialise push subscription
    if (this.isLoggedIn()) {
      this.initPush();
      this.registerDevice();
      this.loadGroupAccess();
    }

    // Samsung Internet detection: show appropriate guidance banner
    this.initSamsungInternetBanners();

    // Initialise push after each successful login navigation
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd)
    ).subscribe((event) => {
      this.checkAuthPage();
      this.mobileNavOpen = false; // close mobile nav on every route change
      if (this.isLoggedIn() && !this.isAuthPage) {
        this.loadNotifications();
        this.initPush();
        this.registerDevice();
        this.loadGroupAccess();

        // Auto-start onboarding tour on first login (dashboard only)
        const navEnd = event as NavigationEnd;
        if (navEnd.urlAfterRedirects === '/dashboard' && !this.tourService.isTourCompleted()) {
          setTimeout(() => {
            const role = this.auth.getUserRole() || 'Member';
            this.tourService.startTour(role);
          }, 600);
        }
      }
    });

    // Navigation progress bar + route fade counter
    this.notifSub!.add(
      this.router.events.subscribe(event => {
        if (event instanceof NavigationStart) {
          this.navProgressVisible = true;
        } else if (event instanceof NavigationEnd || event instanceof NavigationCancel || event instanceof NavigationError) {
          this.navProgressVisible = false;
          if (event instanceof NavigationEnd) {
            this.routeCounter++;
            // Hide install and update banners on auth pages — they overlay buttons
            const url = (event as NavigationEnd).urlAfterRedirects;
            if (url.startsWith('/login') || url.startsWith('/signup') ||
                url.startsWith('/register') || url.startsWith('/forgot-password') ||
                url.startsWith('/reset-password') || url.startsWith('/verify-email')) {
              this.showInstallBanner = false;
              this.showUpdatePrompt = false;
              this.showSamsungWarnBanner = false;
              this.showSamsungReinstallBanner = false;
            }
          }
        }
      })
    );
  }

  private loadVersion(): void {
    this.http.get<{ frontendVersion: string; backendVersion: string }>(
      `${environment.apiUrl}/app-config/version`
    ).subscribe({
      next: v => { this.version = v.frontendVersion || v.backendVersion || ''; },
      error: () => {
        this.http.get<{ version: string }>('/version.json').subscribe({
          next: v => { this.version = v.version; },
          error: () => {}
        });
      }
    });
  }

  private hideSplash(): void {
    const splash = document.getElementById('app-splash');
    if (!splash) return;
    splash.classList.add('splash-hide');
    setTimeout(() => splash.remove(), 400);
  }

  /** Subscribe to server push (silent — only fires if permission is already granted). */
  private pushInitialised = false;
  private async initPush() {
    if (this.pushInitialised) return;
    this.pushInitialised = true;
    // Skip silently when permission hasn't been granted yet — the login flow requests it
    // via a user gesture. Calling subscribeToServer() without permission would trigger a
    // browser permission prompt outside of a gesture context (and log a noisy warning).
    if (!('Notification' in window) || Notification.permission !== 'granted') return;
    const subscribed = await this.pushService.subscribeToServer();
    if (!subscribed) {
      console.warn('AppComponent: Push subscription failed despite granted permission.');
    }
  }

  // ── Device registration & PWA install banner ─────────────────────────────

  private deviceRegistered = false;
  private registerDevice(): void {
    if (this.deviceRegistered) return;
    this.deviceRegistered = true;
    this.deviceService.register().subscribe({
      // Only try to show banner if beforeinstallprompt already fired before registration completed
      next: () => { if (this.installPromptEvent) this.maybeShowInstallBanner(); },
      error: err => console.warn('Device registration failed:', err)
    });
  }

  @HostListener('window:online')
  onWindowOnline(): void {
    if (this.offlineResetTimer) clearTimeout(this.offlineResetTimer);
    this.offlineResetTimer = setTimeout(() => { this.isOffline = false; }, 2000);
    if (this.isLoggedIn()) {
      this.syncService.syncNow().subscribe();
    }
  }

  @HostListener('window:offline')
  onWindowOffline(): void {
    if (this.offlineResetTimer) clearTimeout(this.offlineResetTimer);
    this.isOffline = true;
  }

  @HostListener('window:beforeinstallprompt', ['$event'])
  onBeforeInstallPrompt(event: Event): void {
    // On desktop we never call .prompt(), so don't call preventDefault() — letting the
    // browser handle the event natively avoids the Chrome DevTools warning
    // "Banner not shown: beforeinstallpromptevent.preventDefault() called".
    if (!DeviceService.isMobileOrTablet) return;
    event.preventDefault();
    this.installPromptEvent = event;
    // Status may not be loaded yet if device registration hasn't returned — that's fine,
    // maybeShowInstallBanner will gate on currentInstallStatus from localStorage cache.
    this.maybeShowInstallBanner();
  }

  @HostListener('window:appinstalled')
  onAppInstalled(): void {
    this.showInstallBanner = false;
    this.installPromptEvent = null;
    this.deviceService.markInstalled().subscribe();
  }

  private maybeShowInstallBanner(): void {
    // Must have the browser prompt event — without it, calling prompt() does nothing
    if (!this.installPromptEvent) return;

    // Only show on mobile/tablet devices
    if (!DeviceService.isMobileOrTablet) return;

    // Never show on auth pages — fixed banners overlap form buttons
    if (this.isAuthPage) return;

    const status = this.deviceService.currentInstallStatus;
    if (status === InstallPromptStatus.Installed) return;
    if (status === InstallPromptStatus.NeverAskAgain) return;
    if (status === InstallPromptStatus.RemindLater) {
      const next = this.deviceService.nextPromptAt;
      if (next && Date.now() < next.getTime()) return;
    }

    // Delay slightly so it doesn't feel jarring; re-confirm event is still held
    setTimeout(() => {
      if (this.installPromptEvent) this.showInstallBanner = true;
    }, 3000);
  }

  installApp(): void {
    if (!this.installPromptEvent) {
      console.warn('installApp: no prompt event available');
      return;
    }
    const prompt: any = this.installPromptEvent;
    // Consume the event immediately so it can't be called twice
    this.installPromptEvent = null;
    prompt.prompt();
    prompt.userChoice.then((result: any) => {
      this.showInstallBanner = false;
      if (result.outcome === 'accepted') {
        this.deviceService.markInstalled().subscribe();
      } else {
        // User dismissed the native prompt — treat as RemindLater
        const remindAt = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000);
        this.deviceService.markRemindLater(remindAt).subscribe();
      }
    });
  }

  dismissInstallBanner(): void {
    this.showInstallBanner = false;
    // Remind again in 3 days
    const remindAt = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000);
    this.deviceService.markRemindLater(remindAt).subscribe();
  }

  neverAskInstall(): void {
    this.showInstallBanner = false;
    this.deviceService.markNeverAskAgain().subscribe();
  }

  // ── Samsung Internet banners ─────────────────────────────────────────────

  private initSamsungInternetBanners(): void {
    if (!DeviceService.isSamsungInternet) return;

    if (!DeviceService.isRunningStandalone && !this.deviceService.samsungWarnBannerDismissed) {
      // User is browsing in Samsung Internet and has not yet installed (or installed badly)
      this.showSamsungWarnBanner = true;
    } else if (DeviceService.isRunningStandalone && !this.deviceService.samsungReinstallBannerDismissed) {
      // App was installed as a PWA through Samsung Internet — reinstall guide
      this.showSamsungReinstallBanner = true;
    }
  }

  openInChrome(): void {
    const url = window.location.href;
    // Android intent deep link — opens the current URL directly in Chrome if installed
    const chromeIntent = `intent://${url.replace(/^https?:\/\//, '')}#Intent;scheme=https;package=com.android.chrome;end`;

    // Attempt the intent. If Chrome is not installed the intent silently fails,
    // so we fall back to copying the URL to clipboard after a short delay.
    window.location.href = chromeIntent;

    setTimeout(() => {
      // If we're still on the same page after 1.5s, Chrome likely wasn't installed.
      // Copy the URL to clipboard so the user can paste it in any browser.
      if (navigator.clipboard) {
        navigator.clipboard.writeText(url).then(() => {
          this.dialogService.alert('Chrome not found', 'The URL has been copied to your clipboard. Paste it into Chrome or another browser to continue.');
        }).catch(() => {});
      }
    }, 1500);
  }

  dismissSamsungWarnBanner(): void {
    this.showSamsungWarnBanner = false;
    this.deviceService.dismissSamsungWarnBanner();
  }

  dismissSamsungReinstallBanner(): void {
    this.showSamsungReinstallBanner = false;
    this.deviceService.dismissSamsungReinstallBanner();
  }

  // ── App update prompt ────────────────────────────────────────────────────

  private async fetchVersionManifest(): Promise<any> {
    try {
      const res = await fetch('/version.json?_=' + Date.now(), { cache: 'no-store' });
      if (!res.ok) return null;
      return await res.json();
    } catch {
      return null;
    }
  }

  /** Returns true if `current` is strictly older than `minimum`. */
  private isVersionOlder(current: string, minimum: string): boolean {
    if (!minimum || minimum === '0.0.0') return false;
    const parse = (v: string) => (v || '0.0.0').split('.').map(n => parseInt(n, 10) || 0);
    const [cMaj, cMin, cPat] = parse(current);
    const [mMaj, mMin, mPat] = parse(minimum);
    if (cMaj !== mMaj) return cMaj < mMaj;
    if (cMin !== mMin) return cMin < mMin;
    return cPat < mPat;
  }

  private async checkStalenessOnStartup(): Promise<void> {
    const manifest = await this.fetchVersionManifest();
    if (!manifest) return;
    if (this.isVersionOlder(environment.version, manifest.minRequired)) {
      this.isBreakingUpdate = true;
      if (!this.isAuthPage) this.showUpdatePrompt = true;
    }
  }

  applyUpdate(): void {
    this.showUpdatePrompt = false;
    if (this.swUpdate.isEnabled) {
      this.swUpdate.activateUpdate().then(() => window.location.reload());
    } else {
      // SW not available (dev mode or unsupported browser) — just reload
      window.location.reload();
    }
  }

  dismissUpdatePrompt(): void {
    if (this.isBreakingUpdate) return; // cannot dismiss breaking updates
    this.showUpdatePrompt = false;
  }


  ngOnDestroy() {
    this.notifSub?.unsubscribe();
    this.pollSub?.unsubscribe();
  }

  private groupNotifications(notifications: NotificationDto[]): NotificationGroup[] {
    const collapsed = this.buildBellNotifications(notifications);

    if (!collapsed.length) {
      return [];
    }

    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
    const groups = new Map<string, BellNotificationItem[]>();

    for (const notification of collapsed) {
      const createdAt = new Date(notification.createdAt);
      let label = 'Earlier';

      if (createdAt >= oneHourAgo) {
        label = 'New';
      } else if (createdAt >= startOfToday) {
        label = 'Today';
      }

      const existing = groups.get(label) ?? [];
      existing.push(notification);
      groups.set(label, existing);
    }

    return ['New', 'Today', 'Earlier']
      .filter(label => groups.has(label))
      .map(label => ({ label, items: groups.get(label)! }));
  }

  private buildBellNotifications(notifications: NotificationDto[]): BellNotificationItem[] {
    const collapsed: BellNotificationItem[] = [];
    const index = new Map<string, BellNotificationItem>();

    for (const notification of notifications) {
      const key = this.getBellNotificationKey(notification);
      const existing = index.get(key);

      if (existing) {
        existing.ids.push(notification.id);
        existing.count += 1;
        existing.message = `${existing.count} similar ${this.getNotificationSummaryLabel(notification.type)} notifications`;
        continue;
      }

      const item: BellNotificationItem = {
        ids: [notification.id],
        message: notification.message,
        createdAt: notification.createdAt,
        type: notification.type,
        deepLinkUrl: notification.deepLinkUrl,
        count: 1
      };

      index.set(key, item);
      collapsed.push(item);
    }

    return collapsed;
  }

  private getBellNotificationKey(notification: NotificationDto): string {
    const entityKey = notification.relatedEntityId ?? 'none';
    const linkKey = notification.deepLinkUrl ?? 'no-link';
    const textKey = this.normalizeNotificationMessage(notification.message);

    return `${notification.type}|${entityKey}|${linkKey}|${textKey}`;
  }

  private normalizeNotificationMessage(message: string): string {
    return message
      .toLowerCase()
      .replace(/\b\d+([.,]\d+)?\b/g, '#')
      .replace(/\s+/g, ' ')
      .trim();
  }

  private getNotificationSummaryLabel(type: NotificationType): string {
    switch (type) {
      case NotificationType.PaymentDue:
        return 'payment reminder';
      case NotificationType.PaymentReceived:
        return 'payment received';
      case NotificationType.CycleCreated:
      case NotificationType.CycleStarted:
      case NotificationType.CycleMidReminder:
      case NotificationType.CycleClosingSoon:
      case NotificationType.CycleClosed:
        return 'cycle update';
      case NotificationType.CyclePaymentMade:
        return 'payment received';
      case NotificationType.SystemRestart:
        return 'system update';
      case NotificationType.GroupInviteReceived:
      case NotificationType.MemberLeftGroup:
      case NotificationType.MemberJoinedGroup:
        return 'group update';
      case NotificationType.DisputeRaised:
      case NotificationType.DisputeUpdated:
        return 'dispute update';
      case NotificationType.ManualReminder:
        return 'payment reminder';
      default:
        return 'notification';
    }
  }

  getNotificationIcon(type: NotificationType): string {
    switch (type) {
      case NotificationType.PaymentDue:
        return '💰';
      case NotificationType.PaymentReceived:
        return '✅';
      case NotificationType.CycleCreated:
        return '🔄';
      case NotificationType.CycleStarted:
        return '🚀';
      case NotificationType.CyclePaymentMade:
        return '💸';
      case NotificationType.CycleMidReminder:
        return '⏳';
      case NotificationType.CycleClosingSoon:
        return '⚠️';
      case NotificationType.CycleClosed:
        return '🔒';
      case NotificationType.SystemRestart:
        return '🔧';
      case NotificationType.GroupInviteReceived:
        return '👥';
      case NotificationType.DisputeRaised:
        return '🚨';
      case NotificationType.DisputeUpdated:
        return '📋';
      case NotificationType.ManualReminder:
        return '📣';
      case NotificationType.MemberLeftGroup:
        return '👋';
      case NotificationType.MemberJoinedGroup:
        return '🎉';
      default:
        return '🔔';
    }
  }

  getNotificationTone(type: NotificationType): 'alert' | 'warning' | 'success' | 'info' | 'neutral' {
    switch (type) {
      case NotificationType.PaymentDue:
        return 'warning';
      case NotificationType.PaymentReceived:
      case NotificationType.CyclePaymentMade:
        return 'success';
      case NotificationType.CycleCreated:
      case NotificationType.CycleStarted:
      case NotificationType.CycleMidReminder:
      case NotificationType.CycleClosed:
      case NotificationType.SystemRestart:
      case NotificationType.GroupInviteReceived:
      case NotificationType.DisputeUpdated:
      case NotificationType.MemberLeftGroup:
      case NotificationType.MemberJoinedGroup:
        return 'info';
      case NotificationType.CycleClosingSoon:
      case NotificationType.ManualReminder:
        return 'warning';
      case NotificationType.DisputeRaised:
        return 'alert';
      default:
        return 'neutral';
    }
  }

  loadNotifications() {
    this.notificationService.load({
      unreadOnly: true,
      take: this.notificationBellLimit
    }).subscribe();
  }

  toggleNotificationDropdown(): void {
    this.notificationDropdownOpen = !this.notificationDropdownOpen;
    this.dropdownOpen = false;
    this.managementDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.vitalsDropdownOpen = false;
    this.diabetesDropdownOpen = false;
    if (this.notificationDropdownOpen) {
      this.loadNotifications();
    }
  }

  markNotifRead(notification: BellNotificationItem): void {
    const request$ = notification.ids.length > 1
      ? this.notificationService.markReadMany(notification.ids)
      : this.notificationService.markRead(notification.ids[0]);

    request$.subscribe({
      next: () => {
        if (notification.deepLinkUrl) {
          this.notificationDropdownOpen = false;
          this.router.navigateByUrl(notification.deepLinkUrl);
        }
      }
    });
  }

  markAllNotifRead(): void {
    this.notificationService.markAllRead().subscribe();
  }

  viewAllNotifications(): void {
    this.notificationDropdownOpen = false;
    this.mobileNavOpen = false;
    this.router.navigate(['/notifications']);
  }
  
  checkAuthPage(): void {
    const url = this.router.url;
    this.isAuthPage = url.includes('/login') || url.includes('/signup') || url.includes('/change-expired-password') || url.includes('/forgot-password') || url.includes('/reset-password');
  }
  
  shouldShowNavigation(): boolean {
    return !this.isAuthPage && this.isLoggedIn();
  }
  
  isLoggedIn(): boolean {
    return !!this.auth.getToken();
  }
  
  getUserDisplayName(): string {
    return this.auth.getUserDisplayName();
  }
  
  isSupportWorker(): boolean {
    return false;
  }
  
  isAdmin(): boolean {
    return this.auth.isAdmin();
  }

  isAdminOrAbove(): boolean {
    return this.auth.isAdminOrAbove();
  }

  canAccessAll(): boolean {
    return this.auth.isAdminOrAbove();
  }

  isCareRecipient(): boolean {
    return false;
  }

  canSeeDiabetesMenu(): boolean {
    return false;
  }

  canSeeIncidentsMenu(): boolean {
    return false;
  }

  canSeeBloodPressureMenu(): boolean {
    return false;
  }

  canSeeSuppliesMenu(): boolean {
    return false;
  }

  isDiabetesCareRecipient(): boolean {
    return false;
  }

  isDiabetesCarer(): boolean {
    return false;
  }

  isDiabetesHealthCareProvider(): boolean {
    return false;
  }

  isDiabetesSupportWorker(): boolean {
    return false;
  }

  isDiabetesAligned(): boolean {
    return false;
  }

  canAccessMealAndBolus(): boolean {
    return false;
  }

  canViewCareRecipients(): boolean {
    return false;
  }

  navigateToMyConditions(): void {
    this.closeDropdown();
  }

  navigateToMyReports(): void {
    this.closeDropdown();
    this.router.navigate(['/profile/my-reports']);
  }

  toggleDiabetesDropdown(): void {
    this.diabetesDropdownOpen = !this.diabetesDropdownOpen;
    this.dropdownOpen = false;
    this.bglDropdownOpen = false;
    this.managementDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }

  closeDiabetesDropdown(): void {
    this.diabetesDropdownOpen = false;
  }

  navigateToAssessment(): void {
    this.router.navigate(['/dashboard']);
  }

  navigateToIncidents(): void {
    this.router.navigate(['/dashboard']);
  }

  navigateToMealEntry(): void {
    this.router.navigate(['/dashboard']);
  }

  navigateToBloodPressure(): void {
    this.router.navigate(['/dashboard']);
  }

  isAdminOrSuperAdmin(): boolean {
    return this.auth.isAdminOrAbove();
  }

  canManageGroupsOrAdmin(): boolean {
    return this.auth.isAdminOrAbove() || this.canManageGroups;
  }

  private loadGroupAccess(): void {
    if (this.auth.isAdminOrAbove()) { this.canManageGroups = true; return; }
    this.groupService.getGroups().subscribe({
      next: groups => { this.canManageGroups = groups.some(g => g.canManage); },
      error: () => {}
    });
  }


  
  toggleDropdown(): void {
    this.dropdownOpen = !this.dropdownOpen;
    this.bglDropdownOpen = false;
    this.managementDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }
  
  closeDropdown(): void {
    this.dropdownOpen = false;
  }
  
  toggleVitalsDropdown(): void {
    this.vitalsDropdownOpen = !this.vitalsDropdownOpen;
    this.dropdownOpen = false;
    this.bglDropdownOpen = false;
    this.managementDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }

  closeVitalsDropdown(): void {
    this.vitalsDropdownOpen = false;
  }

  toggleBglDropdown(): void {
    this.bglDropdownOpen = !this.bglDropdownOpen;
    this.dropdownOpen = false;
    this.vitalsDropdownOpen = false;
    this.managementDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }
  
  closeBglDropdown(): void {
    this.bglDropdownOpen = false;
  }
  
  toggleManagementDropdown(): void {
    this.managementDropdownOpen = !this.managementDropdownOpen;
    this.dropdownOpen = false;
    this.bglDropdownOpen = false;
    this.vitalsDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }
  
  closeManagementDropdown(): void {
    this.managementDropdownOpen = false;
    this.managementSubGroupOpen = null;
  }

  toggleManagementSubGroup(group: string): void {
    this.managementSubGroupOpen = this.managementSubGroupOpen === group ? null : group;
  }

  toggleSystemDropdown(): void {
    this.systemDropdownOpen = !this.systemDropdownOpen;
    this.dropdownOpen = false;
    this.bglDropdownOpen = false;
    this.vitalsDropdownOpen = false;
    this.managementDropdownOpen = false;
    this.helpDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }

  closeSystemDropdown(): void {
    this.systemDropdownOpen = false;
  }

  toggleHelpDropdown(): void {
    this.helpDropdownOpen = !this.helpDropdownOpen;
    this.dropdownOpen = false;
    this.bglDropdownOpen = false;
    this.vitalsDropdownOpen = false;
    this.diabetesDropdownOpen = false;
    this.managementDropdownOpen = false;
    this.systemDropdownOpen = false;
    this.notificationDropdownOpen = false;
  }

  closeHelpDropdown(): void {
    this.helpDropdownOpen = false;
  }

  /** Called from the Help → Take a Tour menu item. Always relaunches, ignoring completion flag. */
  startTour(): void {
    this.helpDropdownOpen = false;
    const role = this.auth.getUserRole() || 'Member';
    this.tourService.startTour(role);
  }

  navigateToBglReading(): void {
    this.closeBglDropdown();
    this.router.navigate(['/dashboard']);
  }
  
  navigateToBglHistory(): void {
    this.closeBglDropdown();
    this.router.navigate(['/dashboard']);
  }
  
  navigateToUsers(): void {
    this.closeManagementDropdown();
    this.router.navigate(['/management/users']);
  }
  
  navigateToGroups(): void {
    this.mobileNavOpen = false;
    this.router.navigate(['/groups']);
  }

  navigateToCycles(): void {
    this.closeManagementDropdown();
    this.router.navigate(['/cycles']);
  }

  navigateToAppConfig(): void {
    this.closeManagementDropdown();
    this.router.navigate(['/management/app-config']);
  }

  navigateToFeatureBugReports(): void {
    this.closeManagementDropdown();
    this.router.navigate(['/management/feature-bug-reports']);
  }

  navigateToProfile(): void {
    this.closeDropdown();
    this.router.navigate(['/profile']);
  }
  
  navigateToMfaSetup(): void {
    this.closeDropdown();
    this.router.navigate(['/profile/security']);
  }
  
  logout(): void {
    this.closeDropdown();
    // Unsubscribe from push before clearing the token (needs the JWT for auth).
    this.pushService.unsubscribeFromServer().catch(() => {});
    this.auth.clearToken();
    this.router.navigate(['/login']);
  }

  restartSystem(): void {
    this.closeManagementDropdown();
    this.dialogService.confirm({
      title: 'Restart API Service',
      message: 'Are you sure you want to restart the API service? Active requests will be interrupted.',
      confirmText: 'Restart',
      cancelText: 'Cancel',
      confirmColor: 'warn'
    }).subscribe(confirmed => {
      if (!confirmed) return;
      this.systemService.restart().subscribe();
    });
  }
}