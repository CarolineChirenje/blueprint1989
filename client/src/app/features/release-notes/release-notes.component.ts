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
  readonly appName = 'Batanai';

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
      date: '2026-04-04',
      status: 'latest',
      summary: 'Full platform launch — expense cycle management, expense and payment tracking, member obligations, push notifications, offline queue, biometric auth, TOTP MFA, and comprehensive admin management.',
      sections: [
        {
          type: 'security',
          label: 'Security & Authentication',
          icon: 'security',
          items: [
            {
              title: 'Authentication & Session Management',
              description: 'Signup, login, password expiry enforcement, and forced password change with JWT-based session management. Includes a forgot-password email flow with time-limited reset tokens.',
              technical: 'Custom User entity with BCrypt password hashing via PasswordHashingService. JWT issued with configurable expiry (AppConfig: JwtExpirationMinutes). Login: POST /api/auth/login → 200 + { token } | 401. Forced change: mustChangePassword flag detected in AuthGuard, routed to /auth/change-expired-password. Forgot password: time-limited PasswordResetToken sent via SMTP EmailService. Angular AuthInterceptor attaches Bearer token to all outbound requests.'
            },
            {
              title: 'TOTP Multi-Factor Authentication',
              description: 'Users can enrol in TOTP-based MFA from profile settings, generating a QR code for an authenticator app and a set of single-use backup codes. MFA can be confirmed, used at login, and disabled at any time.',
              technical: 'MfaService generates TOTP secret and QR code (QRCoder) returned as Base64 PNG. TOTP verify: POST /api/auth/verify-mfa. If MFA enabled, login issues a short-lived mfa_temp token; full JWT issued only after TOTP confirmation. Backup codes generated at enrolment. IsMfaEnabled + MfaSecret on User entity.'
            },
            {
              title: 'WebAuthn / Biometric Authentication',
              description: 'Users can register platform biometrics (Face ID, Touch ID, Windows Hello) as a second-factor or passwordless credential. Multiple credentials can be enrolled and individually revoked from profile settings.',
              technical: 'WebAuthnController backed by Fido2NetLib. Registration: POST /api/webauthn/registration/begin → /complete. Authentication: /authentication/begin → /complete. WebAuthnCredential stores CredentialId (base64url), COSE public key, sign counter, AAGUID. Cloned-authenticator detection via monotonic sign counter check.'
            },
            {
              title: 'Role-Based Access Control (RBAC)',
              description: 'Three-tier role hierarchy — SuperAdmin, Admin, and Member — controls access to every feature, screen, and API endpoint.',
              technical: 'Named authorization policies registered in Program.cs: SuperAdminOnly, AdminOrAbove, MemberOrAbove. Role enum embedded as integer claim in JWT. Angular RoleGuard reads roles from token; routes use data: { roles: [...] } for declarative RBAC. Unauthorised: 403 Forbidden with ProblemDetails.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Expense Management',
          icon: 'receipt_long',
          items: [
            {
              title: 'Expense Cycle Management',
              description: 'Admins create named expense cycles with a start and end date to scope shared expenses into structured billing periods. Cycles can be closed once all payments are settled, at which point member balances are calculated and locked.',
              technical: 'ExpenseCycleController: GET list, GET /{id}, POST, PUT /{id}, POST /{id}/close, GET /{id}/members, POST /{id}/members, DELETE /{id}/members/{userId}. ExpenseCycle entity: Name, StartDate, EndDate, Status (Active/Closed). CloseAsync calculates net MemberObligation rows from all Expense and Payment records in the cycle.'
            },
            {
              title: 'Expense Tracking',
              description: 'Members record shared expenses against an active cycle, specifying amount, category, payer, and description. Expenses can be edited or deleted while the cycle is open.',
              technical: 'ExpenseController: POST, GET, GET /{id}, PUT /{id}, DELETE /{id} — all filtered by cycleId. Expense entity: Amount (decimal), Category (Rent/Utilities/Groceries/Transport/Entertainment/Other), Description, PaidByUserId, CycleId, CreatedAt. AdminOrAbove can delete any expense; members can delete their own.'
            },
            {
              title: 'Payment Management',
              description: 'Members submit payments to settle obligations. Each payment follows a confirmation workflow: the recipient must confirm or reject the payment before the balance is updated.',
              technical: 'PaymentController: POST, GET, GET /{id}, POST /{id}/confirm, POST /{id}/reject, DELETE /{id}. Payment entity: Amount, FromUserId, ToUserId, CycleId, Status (Pending/Confirmed/Rejected), Notes. Status transitions enforced server-side; only the recipient (ToUserId) can confirm or reject.'
            },
            {
              title: 'Member Obligations',
              description: 'Each cycle member\'s net balance — how much they owe or are owed — is computed and displayed in real time. Admins can view the full obligation matrix for the cycle.',
              technical: 'MemberObligation entity: CycleId, UserId, Amount (positive = owed to member, negative = member owes). Recalculated on cycle close by summing expense shares and confirmed payments. Front-end ObligationsComponent shows per-member breakdown with colour-coded balance pill.'
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
              description: 'Bell icon in the navigation bar shows unread in-app notifications for key events — payment due, payment received, cycle created, and system messages. Notifications can be marked read individually or all at once.',
              technical: 'NotificationController: GET (paginated), PUT {id}/read, PUT /read-all. Notification entity: UserId, Message, IsRead, Type (enum 1–5), DeepLinkUrl, RelatedEntityId, SentViaPush. NotificationTypeEntity seeded with 5 types. Angular NotificationService polls unread count every 60 seconds.'
            },
            {
              title: 'Web Push Notifications',
              description: 'Browser push notifications for payment-due reminders, payment confirmations, new cycle announcements, and system restart warnings. Per-user opt-in preferences configurable from profile settings.',
              technical: 'PushController: GET /api/push/vapid-public-key, POST /subscribe, DELETE /unsubscribe, POST /test. PushSubscription entity stores endpoint + p256dh + auth keys. PushNotificationSender uses Lib.Net.Http.WebPush with VAPID. Notification types: General(1), PaymentDue(2), PaymentReceived(3), CycleCreated(4), SystemRestart(5).'
            },
            {
              title: 'Notification Preferences',
              description: 'Users configure which push notification types they receive. Preferences can be toggled per notification type from profile settings.',
              technical: 'NotificationPreferenceController: GET /api/notification-preference, PUT (per-type). NotificationPreference entity: UserId, NotificationTypeId, IsEnabled. Seeded with 5 preference rows per user on signup. Angular NotificationPreferencesComponent in Profile → Notifications.'
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
              description: 'Users can view and update their personal details, change their password, and manage security settings from a unified profile page.',
              technical: 'POST /api/auth/profile GET/PUT. UpdateProfileRequest validated with FluentValidation. Password change: POST /api/auth/change-password with current-password verification before hash update. Profile lazy-loaded in ProfileModule; all sub-pages behind AuthGuard.'
            },
            {
              title: 'Linked Devices',
              description: 'Users can view all registered devices (browser/PWA installs), rename them, and remove devices that are no longer in use.',
              technical: 'UserDeviceController: POST /api/userdevice/register, PATCH /install-status, GET, PATCH {id}/rename, DELETE {id}. UserDevice entity tracks FriendlyName, InstallStatus, DeviceType, UserAgent.'
            },
            {
              title: 'My Feature & Bug Reports',
              description: 'Users can view the history of their own submitted feature requests and bug reports, including current status and priority, from the profile section.',
              technical: 'FeatureBugReportController: GET /api/feature-bug-report/my-reports (filtered by authenticated UserId). Angular MyReportsComponent in ProfileModule.'
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
              description: 'Batanai is fully installable as a PWA on mobile and desktop. A custom install prompt encourages installation with RemindLater and NeverAskAgain options. Device registration tracks install status per user per device.',
              technical: '@angular/service-worker with ngsw-config.json (app-shell prefetch; API routes on network-first freshness). Custom custom-sw.js handles push events. manifest.webmanifest: display standalone, 192×192 and 512×512 icons. DeviceService manages BeforeInstallPromptEvent.'
            },
            {
              title: 'Offline Entry Queue',
              description: 'Expenses and payments submitted without connectivity are queued in IndexedDB and automatically synced when the connection is restored.',
              technical: 'OfflineQueueService uses IndexedDB (idb) DB name batanaiDb to persist pending requests with a 24-hour TTL. Queue types: expense, payment. SyncService listens to navigator.onLine events; queued items replayed sequentially on reconnect. Failed items retained with error state for manual review.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Administration',
          icon: 'admin_panel_settings',
          items: [
            {
              title: 'App Configuration',
              description: 'Administrators manage runtime settings — JWT expiry, password policy, SMTP credentials, VAPID keys, and more — from the management UI without redeploying.',
              technical: 'AppConfigController: GET /api/appconfig/version (public), GET list, GET {key}, PUT {key}, PUT /bulk (AdminOrAbove). AppConfigEntry: Key, Value, DataType, Category, DisplayName, IsSecret, IsReadOnly. Secrets masked in GET responses.'
            },
            {
              title: 'Feature & Bug Reporting',
              description: 'All users can submit feature requests and bug reports via a persistent floating button accessible throughout the app. Admins can view, prioritise, change status, and delete all reports from the management console.',
              technical: 'FeatureBugReportController: POST, GET, GET {id}, PUT {id}, DELETE {id}, GET /my-reports. Report entity: Title, Description, TypeId, PriorityId, StatusId, VersionNumber, multi-category via join table. ReportFeatureBugComponent rendered at AppModule level.'
            },
            {
              title: 'Admin Management Console',
              description: 'A centralised, role-gated management section covering user management, cycle management, app configuration, and feature/bug report review.',
              technical: 'ManagementModule (lazy-loaded, RoleGuard: AdminOrAbove) hosts: UserManagementComponent, CycleManagementComponent, AppConfigManagementComponent, FeatureBugReportsComponent.'
            }
          ]
        }
      ],
      techStack: {
        frontend: ['Angular 21', 'Angular Material 21', 'Angular PWA / Service Worker', 'RxJS 7', 'TypeScript 5.9', 'idb (IndexedDB)'],
        backend: ['.NET 10', 'ASP.NET Core Web API', 'Entity Framework Core 10', 'PostgreSQL (Npgsql)', 'JWT Bearer Auth', 'Fido2NetLib (WebAuthn)', 'QRCoder (TOTP MFA)', 'Lib.Net.Http.WebPush (VAPID)', 'SMTP (EmailService)'],
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
