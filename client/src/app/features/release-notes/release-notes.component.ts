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
  readonly appName = 'Divvy';

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
      version: '1.0.2',
      date: '2026-03-27',
      status: 'latest',
      summary: 'Excel export and Google Drive upload for Blood Pressure and BGL Assessment history, plus PWA update banner polish and push notification resilience.',
      sections: [
        {
          type: 'added',
          label: 'Added',
          icon: 'add_circle',
          items: [
            {
              title: 'Blood Pressure — Excel Export',
              description: 'Download the current cycle\'s blood pressure session history as an Excel workbook directly from the BP History page. The workbook contains two sheets: a Session Summary with per-session averages, category, severity, and medication status; and Individual Readings with every raw reading timestamp and value.',
              technical: 'GET /api/export/bp-report?termId= streams an .xlsx file built by ExportService.GenerateBpExcelReportAsync(). Reads BloodPressureSessions filtered by termId, deserialises ReadingsJson (camelCase + PascalCase), converts timestamps via TimeZoneService. Angular BloodPressureService.exportReport() fetches responseType:\'blob\'; BpHistoryComponent.exportExcel() triggers browser download via URL.createObjectURL().'
            },
            {
              title: 'Blood Pressure — Upload to Google Drive',
              description: 'Users with a connected Google Drive account can push the same BP Excel workbook directly to their Drive from the BP History page. The Google Drive button appears automatically when a Drive connection is detected.',
              technical: 'POST /api/export/upload-bp-to-drive?termId= reads userId from JWT, generates BP report, calls ExportService.UploadToGoogleDriveAsync(). Angular BloodPressureService.uploadToDrive() posts to the endpoint. BpHistoryComponent checks GoogleDriveService.getOwnStatus() in ngOnInit to conditionally show the Drive button. Success/error messages auto-dismiss after 5–6 seconds.'
            },
            {
              title: 'BGL Assessment — Upload to Google Drive',
              description: 'Users with a connected Google Drive account can upload the BGL Assessment History Excel report to their Drive directly from the Assessment History page. The Drive button appears only when a connection is active.',
              technical: 'POST /api/export/upload-assessment-to-drive?termId= generates the existing AssessmentExcelReport and uploads via ExportService.UploadToGoogleDriveAsync(). AssessmentService.uploadToDrive() added to call endpoint. AssessmentHistoryComponent gains driveConnected, uploadingToDrive, and successMessage state; GoogleDriveService.getOwnStatus() checked in ngOnInit. CSS adds .drive-upload-btn and .success-message styles with mobile touch targets.'
            }
          ]
        },
        {
          type: 'fixed',
          label: 'Fixed',
          icon: 'bug_report',
          items: [
            {
              title: 'PWA Update Banner — Mobile Layout',
              description: 'The update-available banner now stacks vertically on small screens with full-width buttons, preventing text overflow and overlapping controls on mobile devices.',
              technical: 'Added @media (max-width: 480px) rule to app.component.css targeting .pwa-update-banner: flex-direction column, full-width buttons.'
            },
            {
              title: 'PWA Update Banner — Immediate Dismiss on Apply',
              description: 'Tapping "Apply Update" now immediately hides the update banner before the page reloads, eliminating the brief flash where both the banner and the reload were visible simultaneously.',
              technical: 'applyUpdate() in AppComponent now sets this.showUpdatePrompt = false as its first statement before calling SwUpdate.activateUpdate().'
            },
            {
              title: 'Push Notification VAPID Resilience',
              description: 'A misconfigured VAPID Subject (e.g. missing mailto: prefix in environment secrets) no longer causes the DI container to fail at startup, which previously resulted in 504 Gateway Timeout errors on all endpoints that inject IPushNotificationSender.',
              technical: 'PushNotificationSender constructor now validates vapid.Subject: if null, empty, or missing a mailto:/https: scheme prefix it logs a warning, sets _pushClient = null, and returns early without throwing. SendToUserAsync() guards with if (_pushClient is null) return early so in-app notifications are still created. The ArgumentException from VapidAuthentication.set_Subject is no longer propagated to the DI resolution phase.'
            }
          ]
        }
      ],
      techStack: {
        frontend: ['Google Drive API v3 (BGL Assessment upload)'],
        backend: ['ExportController: upload-assessment-to-drive + upload-bp-to-drive endpoints', 'ExportService.GenerateBpExcelReportAsync (2-sheet workbook)', 'PushNotificationSender: null-safe VAPID Subject validation']
      }
    },
    {
      version: '1.0.1',
      date: '2026-03-27',
      status: 'stable',
      summary: 'Guided system tour, role-aware What\'s New announcements, and blood pressure incident management.',
      sections: [
        {
          type: 'added',
          label: 'Added',
          icon: 'add_circle',
          items: [
            {
              title: 'Guided System Tour',
              description: 'A role-aware interactive walkthrough powered by Shepherd.js automatically launches the first time a user lands on the dashboard after login. The tour highlights key areas of the app relevant to the user\'s role â€” care user features for recipients, carers, and support workers; admin features for Administrators and SuperAdmins. Users can replay the tour at any time from Help â†’ Take a Tour.',
              technical: 'TourService (client/src/app/core/services/tour.service.ts) builds role-specific step arrays using buildCareUserSteps() and buildAdminSteps(). Steps use Shepherd.js default export (ESM-only: import Shepherd from \'shepherd.js\'; Tour constructed via new (Shepherd as any).Tour(...)). On mobile, steps detach from DOM anchors and use a centred overlay. Steps filtered at runtime to skip anchors that are not present in the DOM for the current role (e.g. #nav-management only visible to admins). Tour completion persisted via POST /api/auth/tour/complete â†’ sets HasCompletedTour = true on the User entity. AppComponent.ngOnInit() wires a NavigationEnd subscriber: if user.hasCompletedTour === false on /dashboard, launchTour() is called. Dedicated "Inspired by Arya â¤ï¸" final step. Shepherd CSS fully themed to Vitara blue in styles.css.'
            },
            {
              title: 'What\'s New Announcements',
              description: 'When a returning user logs in after a platform version update, a short "What\'s New" mini-tour automatically appears on their first visit to the dashboard, summarising new features in the current release. The prompt only appears once per version per user and is skippable at any step.',
              technical: 'Version tracking stored on User entity as LastSeenVersion (nullable string, max 20 chars; EF migration AddLastSeenVersion). Exposed via AuthResponse DTO and persisted to localStorage/sessionStorage on login alongside hasCompletedTour. AppComponent checks user.lastSeenVersion !== environment.version on /dashboard NavigationEnd; if user has already completed the main tour, startWhatsNewTour() is called instead of the main tour. TourService.startWhatsNewTour(version, role, callbacks) builds a 4-step mini-tour via buildWhatsNewSteps(). Acknowledgement persisted via POST /api/auth/seen-version (SeenVersionRequest DTO { Version: string }). Cancelling or completing the tour both call markVersionSeen() so the tour does not reappear.'
            },
            {
              title: 'Blood Pressure Incident Management',
              description: 'Submit, view, and manage blood pressure incidents with severity classification, carer comments, and weekly statistics. Separate incident workflow from routine BP sessions for targeted care-team response.',
              technical: 'BpIncidentController (api/bp-incident): POST/GET/GET{id}/PUT/DELETE entries, PATCH {id}/carer-comment, GET /weekly. BpIncident entity: Systolic, Diastolic, PulseRate, BpCategory, Severity, MedicationAdministered, RequiredSupervision, CycleId. Gated to SupportOrHigher; DELETE to AdminOrHigher.'
            }
          ]
        }
      ],
      techStack: {
        frontend: ['Shepherd.js (ESM, guided tour)'],
        backend: ['EF Core migration: AddHasCompletedTour, AddLastSeenVersion']
      }
    },
    {
      version: '1.0.0',
      date: '2026-03-10',
      status: 'stable',
      summary: 'Full platform launch â€” BGL and blood pressure tracking, temperature monitoring, weight tracking with BMI, meal logging, incident management, supplies, push notifications, offline queue, biometric auth, TOTP MFA, vitals charts, data export, Google Drive integration, and comprehensive admin management.',
      sections: [
        {
          type: 'security',
          label: 'Security & Authentication',
          icon: 'security',
          items: [
            {
              title: 'Authentication & Session Management',
              description: 'Signup, login, password expiry enforcement, and forced password change with JWT-based session management. Includes a forgot-password email flow with time-limited reset tokens.',
              technical: 'Custom User entity with BCrypt password hashing via PasswordHashingService. JWT issued with configurable expiry (AppConfig: JwtExpirationMinutes). Login flow: POST /api/auth/login â†’ 200 + { token } | 401. Forced change: mustChangePassword flag detected in AuthGuard, routed to /auth/change-expired-password. Forgot password: time-limited PasswordResetToken sent via SMTP EmailService. Angular AuthInterceptor attaches Bearer token to all outbound requests.'
            },
            {
              title: 'TOTP Multi-Factor Authentication',
              description: 'Users can enrol in TOTP-based MFA from profile settings, generating a QR code for an authenticator app and a set of single-use backup codes. MFA can be confirmed, used at login, and disabled at any time.',
              technical: 'MfaService generates TOTP secret and QR code (QRCoder) returned as Base64 PNG. TOTP verify: POST /api/auth/verify-mfa. If MFA enabled, login issues a short-lived mfa_temp token; full JWT issued only after TOTP confirmation. Backup codes generated at enrolment. Enrolment state: IsMfaEnabled + MfaSecret on User entity.'
            },
            {
              title: 'WebAuthn / Biometric Authentication',
              description: 'Users can register platform biometrics (Face ID, Touch ID, Windows Hello) as a second-factor or passwordless credential. Multiple credentials can be enrolled and individually revoked from profile settings.',
              technical: 'WebAuthnController.cs backed by Fido2NetLib. Registration: POST /api/webauthn/registration/begin â†’ POST /api/webauthn/registration/complete. Authentication: /authentication/begin â†’ /authentication/complete. WebAuthnCredential entity stores CredentialId (base64url), COSE public key, sign counter, and AAGUID. Cloned-authenticator detection via monotonic sign counter check. Credential management: GET /api/webauthn/credentials, DELETE /api/webauthn/credentials/{id}.'
            },
            {
              title: 'Role-Based Access Control (RBAC)',
              description: 'Six-tier role hierarchy â€” SuperAdmin, Administrator, Carer, SupportWorker, CareRecipient, and HealthCareProvider â€” controls access to every feature, screen, and API endpoint.',
              technical: 'Named authorization policies registered in Program.cs: SuperAdminOnly, AdminOrHigher, SupportOrHigher, AllRoles. Role enum embedded as integer claim in JWT. Angular RoleGuard reads roles from token; routes use data: { roles: [...] } for declarative RBAC. Unauthorised: 403 Forbidden with ProblemDetails.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Health Monitoring',
          icon: 'monitor_heart',
          items: [
            {
              title: 'BGL Assessment Module',
              description: 'Record and track blood glucose assessments with sensor and blood-test readings, treatment cycles, insulin administration, and timer-driven recheck reminders. Readings are automatically classified as Normal, Hypo, or Hyper against configurable thresholds.',
              technical: 'AssessmentController: POST/GET /api/assessment, GET /api/assessment/{id}, GET /api/assessment/user/{userId}. Assessment entity: InitialReading, AssessmentType (Normal/Hypo/Hyper), BloodTestReading, RecheckReading, TreatmentCycles, KetoneLevel, AssessmentOutcome, DurationSeconds, AllReadingsJson, CycleDetailsJson. BglClassificationService classifies reading against admin-configured BglClassificationRange + KetoneThreshold rows. AppConfig keys: BglRecheckWaitMinutes, BglAdditionalWaitMinutes, HyperGlucoseReminderIntervalMinutes, HypoMaxTreatmentCycles.'
            },
            {
              title: 'Blood Pressure Monitoring',
              description: 'Record multi-reading BP sessions with automatic systolic/diastolic averaging, pulse rate tracking, and medication status. Results are classified from Hypotension to Hypertensive Crisis. History includes weekly average trends.',
              technical: 'BloodPressureController: POST/GET /api/blood-pressure, GET /{id}, GET /weekly-averages. BloodPressureSession stores up to AppConfig:BpReadingCount individual readings; averages computed at save. BpClassificationService maps averaged readings to BpCategory against admin-configured BpClassificationRange rows. PulseRateClassificationService classifies pulse rate. MedicationStatus enum: Before/After/AboutTo/None/NoMedicationConsumed.'
            },
            {
              title: 'Temperature Monitoring',
              description: 'Record body temperature readings in Celsius with measurement site selection and automatic classification from Hypothermia to Hyperpyrexia. Administrators can configure per-care-recipient classification ranges that override global defaults.',
              technical: 'TemperatureController: POST/GET /api/temperature, GET /{id}, DELETE /{id}. TemperatureReading entity: TemperatureCelsius (numeric 4,1), Category (enum: Hypothermia â†’ Hyperpyrexia), Severity, Label, Site (enum: Oral/Axillary/Tympanic/Rectal/Temporal), Notes, CareRecipientId, TermId. TemperatureClassificationService resolves effective ranges â€” falls back to global defaults when no per-care-recipient override exists. DatabaseSeeder seeds 7 default ranges (35.0 Â°C Hypothermia to 41.1 Â°C Hyperpyrexia).'
            },
            {
              title: 'Weight Tracking & BMI',
              description: 'Record weight readings in kilograms with automatic BMI calculation when a height is on file. BMI category (Underweight through Morbidly Obese) is recorded alongside each entry. History includes table and chart views.',
              technical: 'WeightController: POST/GET /api/weight, GET /{id}, DELETE /{id}. WeightReading entity: WeightKg (numeric 5,1), Bmi (numeric 4,1, nullable), BmiCategory, Notes, CareRecipientId, TermId. BMI computed at save using User.HeightCm (numeric 5,1); HeightCm exposed in UpdateProfileRequest for care recipients and admins.'
            },
            {
              title: 'Meal Entry Module',
              description: 'Log meals with food item, portion size, portion unit, carbohydrate estimate, and carb level classification correlated to care recipients and reporting cycles. Entries can be edited or deleted.',
              technical: 'MealEntryController: POST/GET/PUT/DELETE /api/meal-entry/entries, GET /api/meal-entry/export/weekly. MealEntry entity: FoodItem, PortionSize (numeric 7,2), PortionUnit (enum: Grams/Millilitres/Cups/Tablespoons/Teaspoons/Pieces/Slices/Units), CarbsConsumed, CarbLevel, Timestamp, LastUpdatedBy audit field. Angular reactive form with Validators.min/max for carb range.'
            },
            {
              title: 'Vitals Charts',
              description: 'Interactive line charts for Blood Pressure, BGL, Temperature, and Weight history with colour-coded reference band lines that mirror classification range boundaries. Each history page includes a Table / Chart toggle.',
              technical: 'Shared VitaraChartComponent wraps Chart.js 4.x directly (no ng2-charts). Accepts title, labels, datasets (VitaraChartDataset[]), yAxisLabel, and referenceBands (VitaraReferenceBand[]). Custom vitaraRefLines plugin draws dashed horizontal reference lines with labels. BP chart uses weekly-average data; BGL, Temperature and Weight charts summarise history list data client-side. Declared in SharedModule; consumed by all four vitals feature modules.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Safety & Incident Management',
          icon: 'warning',
          items: [
            {
              title: 'BGL Incident Reporting',
              description: 'Submit, manage, and track blood glucose incidents with severity classification, insulin administration details, and carer comments. Weekly statistics provide a summary view for care teams.',
              technical: 'IncidentController (api/incident): POST/GET/GET{id}/PUT/DELETE entries, PATCH {id}/carer-comment, GET /weekly. Incident entity: SensorValue, BloodTestValue, BglState, Severity, InsulinAdministered, treatment fields, CycleId. Gated to SupportOrHigher; DELETE gated to AdminOrHigher.'
            },
            {
              title: 'Blood Pressure Incident Reporting',
              description: 'Submit, manage, and track blood pressure incidents separately from routine sessions. Captures BP readings, pulse rate, severity, medication administered, and whether supervision was required.',
              technical: 'BpIncidentController (api/bp-incident): POST/GET/GET{id}/PUT/DELETE entries, PATCH {id}/carer-comment, GET /weekly. BpIncident entity: Systolic, Diastolic, PulseRate, BpCategory, Severity, MedicationAdministered, RequiredSupervision, CycleId. Gated to SupportOrHigher; DELETE to AdminOrHigher.'
            },
            {
              title: 'Supplies Management',
              description: 'Track medical supplies per care recipient with usage rates, last order dates, and automatic days-until-runout projections. Scheduled weekly supply reports are emailed to administrators.',
              technical: 'SupplyController (api/supply) and SupplyItemController (api/supplyitem): full CRUD. Supply entity: CareRecipientId, SupplyItemId, UsageQuantity, UsageUnit (PerDay/PerWeek/PerMonth/PerYear), LastOrderDate, TotalOrdered; days-remaining derived by SupplyProjectionService. SupplyReportService composes HTML email. AppConfig keys: SupplyReorderLeadDays, SupplyAlertThresholdDays, SupplyReportIntervalDays, SupplyReportExtraEmails.'
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
              description: 'Bell icon in the navigation bar shows unread in-app notifications for key events â€” assessment severity alerts, incident notifications, and system messages. Notifications can be marked read individually or all at once.',
              technical: 'NotificationController: GET /api/notification (paginated), PUT {id}/read, PUT /read-all, POST /ketone-alert. Notification entity: UserId, Message, IsRead, Type (enum), DeepLinkUrl, RelatedEntityId, SentViaPush. NotificationTypeEntity lookup seeded at migration. Angular NotificationService polls unread count every 60 seconds; bell badge driven by interval Observable.'
            },
            {
              title: 'Web Push Notifications & BGL Reminder Timer',
              description: 'Browser push notifications for critical BGL events, assessment reminders, supply alerts, and ketone warnings. Users can schedule a timed BGL recheck push reminder directly from the assessment screen. Per-user opt-in preferences configurable from profile settings.',
              technical: 'PushController: GET /api/push/vapid-public-key, POST /subscribe, DELETE /unsubscribe, POST /test, POST /bg-timer/schedule, DELETE /bg-timer/cancel. PushSubscription entity stores endpoint + p256dh + auth keys. PushNotificationSender uses Lib.Net.Http.WebPush with server VAPID key pair. BgTimerReminder entity: ScheduledAt, SentAt, IsCancelled. BgTimerHostedService (BackgroundService) polls every 30 s. NotificationPreferenceController exposes per-user opt-in per notification type.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Care & User Management',
          icon: 'group',
          items: [
            {
              title: 'Care Recipient Management',
              description: 'Carers link to care recipients, administrators manage those links, and a formal delink request workflow supports the removal of carerâ€“recipient relationships with admin approval.',
              technical: 'CareRecipientController: GET all-active/all (AdminOrHigher), GET my recipients, POST link, GET admin/{id}/linked-carers, DELETE admin/{id}/linked-carer/{carerId}. UserCareRecipient join table. DelinkRequestController: POST (raise request), GET (admin list), POST {id}/approve, POST {id}/reject.'
            },
            {
              title: 'User Conditions & Medications',
              description: 'Users record health conditions (Diabetes Type 1/2, High Blood Pressure) and associated diabetes and BP medications during signup or from the profile page. Conditions drive conditional feature access throughout the app.',
              technical: 'ConditionEntity lookup seeded with DiabetesType1, DiabetesType2, HBP. UserCondition: UserId, ConditionId, YearDiagnosed. UserConditionsController: GET my-conditions, PUT update. AuthService exposes accessibleConditions$ signal. DiabetesMedicationController and BpMedicationController: full CRUD.'
            },
            {
              title: 'Reporting Cycles / Terms',
              description: 'Administrators configure named reporting cycles that scope BGL assessment and incident records for periodic review. A default cycle can be designated and used automatically when no cycle is selected.',
              technical: 'CycleController: GET list, GET /default, POST, PUT {id}, DELETE {id}. Cycle entity: Name, Year, IsDefault. AssessmentService and IncidentService filter by active CycleId when provided. Angular term-selector backed by CycleService Observable.'
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
              description: 'Users can view and update their personal details (name, date of birth, gender, height), change their password, and manage security settings from a unified profile page.',
              technical: 'ProfileController (api/auth/profile): GET, PUT update (name, dob, gender, heightCm). UpdateProfileRequest validated with FluentValidation. Password change: POST /api/auth/change-password with current-password verification before hash update. Profile lazy-loaded in ProfileModule; all sub-pages behind AuthGuard.'
            },
            {
              title: 'Linked Devices',
              description: 'Users can view all registered devices (browser/PWA installs), rename them, and remove devices that are no longer in use.',
              technical: 'UserDeviceController: POST /api/userdevice/register, PATCH /install-status, GET, PATCH {id}/rename, DELETE {id}. UserDevice entity tracks FriendlyName, InstallStatus, DeviceType, UserAgent. Angular LinkedDevicesComponent in ProfileModule.'
            },
            {
              title: 'My Feature & Bug Reports',
              description: 'Users can view the history of their own submitted feature requests and bug reports, including current status and priority, from the profile section.',
              technical: 'FeatureBugReportController: GET /api/feature-bug-report/my-reports (filtered by authenticated UserId). Angular MyReportsComponent in ProfileModule; shares FeatureBugReportService with the admin view.'
            },
            {
              title: 'Notification Preferences',
              description: 'Users configure which types of push notifications they receive. Health-alert notification types are admin-controlled; general preferences can be set independently.',
              technical: 'NotificationPreferenceController: GET /api/notification-preference, PUT (per-type). NotificationPreference entity: UserId, NotificationTypeId, IsEnabled. Admin-controlled types require AdminOrHigher to modify for other users. Angular NotificationPreferencesComponent in Profile â†’ Notifications.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Data & Integrations',
          icon: 'cloud_sync',
          items: [
            {
              title: 'Data Export (Excel)',
              description: 'Export BGL assessments, meal entries, BGL incidents, and BP incidents as Excel spreadsheets filtered by care recipient and date range. Files can be downloaded directly or pushed to Google Drive.',
              technical: 'ExportController: GET /api/export/assessment-report, /incident-report, /mealentry-report accept query params careRecipientId, dateFrom, dateTo. ExportService uses ClosedXML to build .xlsx workbooks streamed as application/vnd.openxmlformats-officedocument.spreadsheetml.sheet. POST /api/export/upload-to-drive, /upload-incident-to-drive/{careRecipientId} upload files to connected Google Drive folder. Angular downloads via Blob + URL.createObjectURL().'
            },
            {
              title: 'Google Drive Integration',
              description: 'Care recipients and admins can connect a Google Drive account to receive automated export uploads directly into a pre-configured Drive folder.',
              technical: 'GoogleDriveAuthController: GET /api/google-drive-auth/connect (OAuth2 initiation), GET /callback (token exchange), GET /status, DELETE /disconnect. UserGoogleDriveToken stores OAuth access/refresh tokens per care recipient. Google.Apis.Drive.v3 SDK used for file upload. AppConfig: GoogleDriveClientId, GoogleDriveClientSecret, GoogleDriveRedirectUri, GoogleDriveFrontendUrl.'
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
              description: 'Vitara is fully installable as a PWA on mobile and desktop. A custom install prompt encourages installation with RemindLater and NeverAskAgain options. Device registration tracks install status per user per device.',
              technical: '@angular/service-worker with ngsw-config.json (app-shell prefetch; API routes on network-first freshness). Custom custom-sw.js handles push events and background sync. manifest.webmanifest: theme_color #1976d2, display standalone, 192Ã—192 and 512Ã—512 icons. DeviceService manages BeforeInstallPromptEvent. UserDeviceController: POST /api/userdevice/register, PATCH /install-status.'
            },
            {
              title: 'Offline Entry Queue',
              description: 'Assessments, meal entries, BGL incidents, BP incidents, and blood pressure sessions submitted without connectivity are queued in IndexedDB and automatically synced when the connection is restored.',
              technical: 'OfflineQueueService uses IndexedDB (idb) to persist pending requests with a 24-hour TTL. Queue types: assessment, incident, meal-entry, blood-pressure, bp-incident. SyncService listens to navigator.onLine events and service worker Background Sync; queued items replayed sequentially on reconnect. Failed items retained with error state for manual review in the Offline Queue viewer.'
            }
          ]
        },
        {
          type: 'added',
          label: 'Administration',
          icon: 'admin_panel_settings',
          items: [
            {
              title: 'Classification Management',
              description: 'Administrators configure BGL ranges, ketone thresholds, blood pressure ranges, pulse rate ranges, and temperature ranges that govern how readings are categorised platform-wide. Per-care-recipient overrides are supported for temperature classifications.',
              technical: 'BglClassificationController: GET /api/bglclassification/effective, full CRUD on /ranges and /ketone-thresholds. BpClassificationRangeController and PulseRateClassificationRangeController: full CRUD. TemperatureClassificationRangeController: full CRUD with optional careRecipientId override. DatabaseSeeder populates default values. Classification services resolve effective set at runtime (per-care-recipient supersedes global).'
            },
            {
              title: 'App Configuration',
              description: 'Administrators manage runtime settings â€” JWT expiry, password policy, SMTP credentials, BGL/BP thresholds, supply alert days, Google Drive config, and more â€” from the management UI without redeploying.',
              technical: 'AppConfigController: GET /api/appconfig/version (public), GET list, GET {key}, PUT {key}, PUT /bulk (AdminOrHigher). AppConfigEntry: Key, Value, DataType, Category, DisplayName, IsSecret, IsReadOnly. Secrets masked in GET responses. Categories: System, Auth, Email, Assessment, BloodPressure, Supplies, GoogleDrive, Docs.'
            },
            {
              title: 'Feature & Bug Reporting',
              description: 'All users can submit feature requests and bug reports via a persistent floating button accessible throughout the app. Administrators can view, prioritise, change status, and delete all reports from the management console.',
              technical: 'FeatureBugReportController: POST, GET, GET {id}, PUT {id}, DELETE {id}, GET /my-reports. Report entity: Title, Description, TypeId, PriorityId, StatusId, VersionNumber, multi-category via FeatureBugReportCategory join table. ReportFeatureBugComponent rendered at AppModule level for global availability.'
            },
            {
              title: 'Admin Management Console',
              description: 'A centralised, role-gated management section covering user management, care recipient management, cycle management, classification configuration, app config, delink requests, conditions, and feature/bug report review.',
              technical: 'ManagementModule (lazy-loaded, RoleGuard: AdminOrHigher) hosts: UserManagementComponent, CareRecipientManagementComponent, CycleManagementComponent, BglClassificationManagementComponent, KetoneClassificationManagementComponent, BpClassificationManagementComponent, PulseRateClassificationManagementComponent, TemperatureClassificationManagementComponent, DelinkRequestManagementComponent, AppConfigManagementComponent, ConditionManagementComponent, FeatureBugReportsComponent.'
            }
          ]
        }
      ],
      techStack: {
        frontend: ['Angular 21', 'Angular Material 21', 'Angular PWA / Service Worker', 'Chart.js 4.x', 'RxJS 7', 'TypeScript 5.9', 'idb (IndexedDB)'],
        backend: ['.NET 10', 'ASP.NET Core Web API', 'Entity Framework Core 10', 'PostgreSQL (Npgsql)', 'JWT Bearer Auth', 'Fido2NetLib (WebAuthn)', 'QRCoder (TOTP MFA)', 'ClosedXML (Excel export)', 'Lib.Net.Http.WebPush (VAPID)', 'Google Drive API v3', 'SMTP (EmailService)'],
        infrastructure: ['IIS / Kestrel', 'Cloudflare (DNS + Proxy)', 'GitHub Actions CI/CD', 'Infisical (Secrets Management)']
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
