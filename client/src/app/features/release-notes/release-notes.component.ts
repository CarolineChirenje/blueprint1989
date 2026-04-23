import { Component, OnInit } from '@angular/core';
import { AuthService } from './../../core/services/auth.service';
import { AppConfigService } from '../../shared/services/app-config.service';
import { environment } from '../../../environments/environment';
export type ChangeType = 'added' | 'changed' | 'fixed' | 'security' | 'breaking';

export interface ChangeItem {
  title: string;
  description: string;
  technical?: string;
}

export interface ChangeSection {
  type: ChangeType;
  label: string;
  icon: string;
  items: ChangeItem[];
}

export interface TechStack {
  frontend?: string[];
  backend?: string[];
  infrastructure?: string[];
}

export interface ReleaseNote {
  version: string;
  date: string;
  status: 'latest' | 'stable' | 'alpha';
  summary: string;
  sections: ChangeSection[];
  techStack?: TechStack;
}

@Component({
  selector: 'app-release-notes',
  standalone: false,
  templateUrl: './release-notes.component.html',
  styleUrls: ['./release-notes.component.css']
})
export class ReleaseNotesComponent implements OnInit {

  readonly appVersion = '1.0.0';
  readonly appName = 'Blueprint1989';

  docsUrl = environment.docsUrl;

  constructor(private authService: AuthService, private appConfigService: AppConfigService) {}

  ngOnInit(): void {
    if (this.canViewDocs) {
      this.appConfigService.getByKey('DocsUrl').subscribe({
        next: (entry) => { if (entry?.value) this.docsUrl = entry.value; },
        error: () => { /* keep environment fallback */ }
      });
    }
  }

  get canViewDocs(): boolean {
    return this.authService.isAdminOrAbove();
  }

  readonly releases: ReleaseNote[] = [
    {
      version: '1.0.0',
      date: '2026-04-23',
      status: 'latest',
      summary: 'Platform foundation release. Delivers the full authentication layer (JWT, TOTP MFA, WebAuthn biometrics), role-based access control, in-app and push notifications, user profile and device management, PWA with offline queue, feature and bug reporting, and a complete administration console.',
      sections: [
        {
          type: 'security',
          label: 'Security & Authentication',
          icon: 'security',
          items: [
            {
              title: 'JWT Authentication & Session Management',
              description: 'Signup, login, password expiry enforcement, and forced password change on next login. Includes a forgot-password email flow with time-limited reset tokens and email address verification on signup.',
              technical: 'Custom User entity with BCrypt password hashing via PasswordHashingService. JWT issued with configurable expiry (AppConfig: JwtExpirationMinutes). mustChangePassword flag detected in AuthGuard and routed to /auth/change-expired-password. Forgot-password: time-limited PasswordResetToken stored and emailed via SMTP EmailService. Angular AuthInterceptor attaches Bearer token to all outbound API requests. POST /api/auth/login returns { token } on success or 401 on failure.'
            },
            {
              title: 'TOTP Multi-Factor Authentication',
              description: 'Users can enrol in TOTP-based MFA from profile settings, generating a QR code for any authenticator app and a set of single-use backup codes. MFA can be confirmed at login, and disabled at any time from profile security settings.',
              technical: 'MfaService generates TOTP secret and QR code (QRCoder) returned as Base64 PNG via POST /api/auth/mfa/setup. TOTP verify: POST /api/auth/verify-mfa. If MFA is enabled, login issues a short-lived mfa_temp JWT; the full session JWT is issued only after successful TOTP confirmation. Backup codes are hashed and stored on the User entity. IsMfaEnabled + MfaSecret + MfaBackupCodes on User.'
            },
            {
              title: 'WebAuthn / Biometric Authentication',
              description: 'Users can register platform biometrics (Face ID, Touch ID, Windows Hello, passkeys) as a second-factor or passwordless login credential. Multiple credentials can be enrolled and individually named and revoked from profile security settings.',
              technical: 'WebAuthnController backed by Fido2NetLib. Registration: POST /api/webauthn/registration/begin and /complete. Authentication: POST /api/webauthn/authentication/begin and /complete. WebAuthnCredential entity stores CredentialId (base64url), COSE public key, sign counter, and AAGUID. Cloned-authenticator detection via monotonic sign-counter check on every assertion.'
            },
            {
              title: 'Role-Based Access Control (RBAC)',
              description: 'Three-tier role hierarchy — SuperAdmin, Admin, and User — controls access to every feature, screen, and API endpoint. SuperAdmin cannot self-register and must be seeded or created by an existing SuperAdmin.',
              technical: 'Role enum: SuperAdmin=1, Admin=2, User=3. Named authorization policies in Program.cs: SuperAdminOnly, AdminOrAbove, UserOrAbove. Role embedded as a string claim in the JWT. Angular RoleGuard reads the role claim from the decoded token; routes declare data: { roles: [...] } for declarative access control. Unauthorised requests return 403 Forbidden.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Notifications',
          icon: 'notifications',
          items: [
            {
              title: 'In-App Notification Inbox',
              description: 'A bell icon in the navigation bar shows unread in-app notifications for platform events. Notifications can be marked read individually or all at once. A full notification history page is available at /notifications.',
              technical: 'NotificationController: GET (paginated, filterable by unread/archived), PUT /{id}/read, PUT /read-all. Notification entity: UserId, Message, IsRead, Type (NotificationType enum), DeepLinkUrl, RelatedEntityId, SentViaPush, IsArchived. Angular NotificationService polls unread count every 60 seconds when logged in. Bell notification groups by time: New, Today, Earlier.'
            },
            {
              title: 'Web Push Notifications (VAPID)',
              description: 'Browser push notifications are delivered for system restart warnings, feature and bug report resolutions, and general platform announcements. Users opt in per browser via the profile notification settings. Admins and developers can send test pushes from /dev/push-test.',
              technical: 'PushController: GET /api/push/vapid-public-key (anonymous), POST /subscribe, DELETE /unsubscribe, POST /test (Dev: all users; Prod: Admin+). PushSubscription entity: UserId, Endpoint, P256dh, Auth, UserAgent. PushNotificationSender uses Lib.Net.Http.WebPush with VAPID signing. NotificationType enum: General=1, SystemRestart=2, FeatureBugReportResolved=3. Server serialises enum as string name (JsonStringEnumConverter); client sends matching string in TestPushRequest.'
            },
            {
              title: 'Push Notification Preferences',
              description: 'Users configure which push notification types they receive from profile settings. Preferences are stored per user per notification type and respected by the server before dispatching any push.',
              technical: 'NotificationPreferenceController: GET /api/notification-preference, PUT (per NotificationTypeId). NotificationPreference entity: UserId, NotificationTypeId, IsEnabled. Rows are seeded on user signup for all active notification types. Angular NotificationPreferencesComponent in Profile > Notifications.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Profile & Account',
          icon: 'manage_accounts',
          items: [
            {
              title: 'User Profile Management',
              description: 'Users can view and update their personal details (name, email), change their password, and view their account role from a unified profile page at /profile.',
              technical: 'GET /api/auth/profile, PUT /api/auth/profile (UpdateProfileRequest). Password change: POST /api/auth/change-password with current-password verification before BCrypt hash update. Profile is lazy-loaded via ProfileModule. All sub-pages are behind AuthGuard.'
            },
            {
              title: 'TOTP MFA & Biometric Setup',
              description: 'Dedicated security sub-pages under /profile/security allow users to enrol in TOTP MFA (with QR code, backup codes), register biometric credentials, and revoke any individual credential.',
              technical: 'MfaSetupComponent: QR code PNG rendered inline, backup codes displayed once on enrolment. BiometricSetupComponent: calls WebAuthn registration flow, lists enrolled credentials with name and AAGUID, supports deletion via DELETE /api/webauthn/{credentialId}.'
            },
            {
              title: 'Linked Devices',
              description: 'Users can view all registered browser and PWA installs, rename them for easy identification, and remove devices that are no longer in use from /profile/devices.',
              technical: 'UserDeviceController: POST /api/userdevice/register (called on login), PATCH /install-status, GET list (authenticated user), PATCH /{id}/rename, DELETE /{id}. UserDevice entity: UserId, FriendlyName, InstallStatus, DeviceType, UserAgent, CreatedAt.'
            },
            {
              title: 'My Feature & Bug Reports',
              description: 'Users can view the history of their own submitted feature requests and bug reports, including current status and priority, from /profile/my-reports.',
              technical: 'GET /api/feature-bug-report/my-reports filters by the authenticated UserId. Angular MyReportsComponent in ProfileModule displays type, priority, status, and version tags per report.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Progressive Web App & Offline',
          icon: 'install_mobile',
          items: [
            {
              title: 'Progressive Web App (PWA)',
              description: 'Blueprint1989 is fully installable as a PWA on Android, iOS, and desktop. A smart install prompt encourages installation with RemindLater (3-day snooze) and NeverAskAgain options. Samsung Internet users receive a browser-specific reinstall guide.',
              technical: '@angular/service-worker with ngsw-config.json (app-shell prefetch strategy; API routes excluded for network-first freshness via custom-sw.js). manifest.webmanifest: display standalone, theme-color, 192x192 and 512x512 icons. DeviceService captures BeforeInstallPromptEvent and manages install-status lifecycle via PATCH /api/userdevice/install-status.'
            },
            {
              title: 'Offline Entry Queue',
              description: 'Requests made without network connectivity are captured in an IndexedDB offline queue and automatically replayed when the connection is restored. The queue UI at /offline-queue shows pending and expired entries with a manual sync trigger.',
              technical: 'OfflineQueueService uses IndexedDB (DB: Blueprint1989Notifications, store: offlineQueue). Queue item types: expense | payment (foundation for upcoming expense management). TTL: 24 hours. SyncService subscribes to navigator.onLine and window:online events; queued items are replayed sequentially via the target API endpoint and method stored in each entry. Background Sync registered via service worker when available.'
            },
            {
              title: 'Onboarding Tour',
              description: 'A guided onboarding tour automatically launches on the first login to the dashboard, walking users through key navigation areas. The tour can be relaunched at any time from the Help menu.',
              technical: 'TourService tracks tour completion in localStorage. TourController: GET /api/tour (fetches tour steps for the user\'s role). Tour auto-starts on first NavigationEnd to /dashboard if isTourCompleted() returns false. Help menu item calls startTour() directly, bypassing the completion flag.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Feature & Bug Reporting',
          icon: 'bug_report',
          items: [
            {
              title: 'In-App Report Submission',
              description: 'All authenticated users can submit feature requests and bug reports via a persistent floating button accessible from every page in the app. Reports include type, priority, version, description, and optional categories.',
              technical: 'POST /api/feature-bug-report. Report entity: Title, Description, TypeId, PriorityId, StatusId, VersionNumber, UserId. Multi-category support via FeatureBugReportCategory join table. ReportFeatureBugComponent rendered at AppModule level so the floating button appears globally.'
            },
            {
              title: 'Admin Report Management',
              description: 'Admins can view, filter, prioritise, update status, and delete all submitted reports from /management/feature-bug-reports. Status updates send a push notification (FeatureBugReportResolved) to the submitting user.',
              technical: 'GET /api/feature-bug-report (Admin+, all reports with filtering), GET /{id}, PUT /{id} (status, priority), DELETE /{id}. On status change to Resolved or Closed, NotificationService creates an in-app notification and PushNotificationSender dispatches a VAPID push of type FeatureBugReportResolved=3 to the submitter.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Administration',
          icon: 'admin_panel_settings',
          items: [
            {
              title: 'User Management',
              description: 'Admins manage all registered users from /management/users — view account details, change roles, reset passwords, and deactivate accounts.',
              technical: 'AuthController: GET /api/auth/users (AdminOrAbove), PUT /api/auth/users/{id} (role, active status), POST /api/auth/users/{id}/reset-password. UserManagementComponent in ManagementModule, protected by RoleGuard (Admin, SuperAdmin).'
            },
            {
              title: 'App Configuration Management',
              description: 'Administrators manage all runtime settings — JWT expiry, password policy, SMTP credentials, VAPID keys, docs URL, and more — from /management/app-config without redeploying the application.',
              technical: 'AppConfigController: GET /api/app-config/version (anonymous), GET list, GET /{key}, PUT /{key}, PUT /bulk, POST /reset/{key} (AdminOrAbove). AppConfigEntry: Key, Value, DataType (int/decimal/bool/string/json), Category, DisplayName, Description, IsSecret (masked in GET), IsReadOnly, RequiresRestart. Secrets are masked in list responses.'
            },
            {
              title: 'System Controls',
              description: 'SuperAdmins can initiate a graceful API service restart from the navigation management menu. A push notification (SystemRestart) is dispatched to all subscribers before the process restarts.',
              technical: 'SystemController: POST /api/system/restart (SuperAdminOnly). Before restarting, PushNotificationSender broadcasts a SystemRestart=2 VAPID push to all active PushSubscription records. Restart is performed via IHostApplicationLifetime.StopApplication() with a short delay to allow the response to flush.'
            }
          ]
        }
      ],
      techStack: {
        frontend: ['Angular 21', 'Angular Material 21', 'Angular PWA / Service Worker', 'RxJS 7', 'TypeScript 5.9', 'IndexedDB (idb)'],
        backend: ['.NET 10', 'ASP.NET Core Web API', 'Entity Framework Core 10', 'PostgreSQL (Npgsql 9)', 'JWT Bearer Auth', 'Fido2NetLib (WebAuthn)', 'QRCoder (TOTP MFA)', 'Lib.Net.Http.WebPush (VAPID)', 'SMTP (EmailService)', 'BCrypt.Net'],
        infrastructure: ['Nginx / Kestrel', 'Cloudflare (DNS + Proxy)', 'GitHub Actions CI/CD', 'Infisical (Secrets Management)']
      }
    }
  ];

  getStatusColor(status: string): string {
    switch (status) {
      case 'latest': return 'accent';
      case 'stable': return 'primary';
      case 'alpha': return 'warn';
      default: return 'primary';
    }
  }

  getTypeClass(type: ChangeType): string {
    return `type-${type}`;
  }
}

