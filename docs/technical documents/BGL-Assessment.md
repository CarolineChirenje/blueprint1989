# BGL Assessment

## Overview

The BGL Assessment feature provides a structured, guided workflow for recording and managing blood glucose level (BGL) readings for care recipients. Rather than a simple single-value entry, the assessment represents a full clinical protocol: an initial sensor or blood-test reading, then a branching state-machine for hypoglycemia (low BGL) or hyperglycemia (high BGL), each with timed treatment cycles, ketone measurements, and recheck loops. All assessments are persisted with full reading timelines and cycle details, and a history page presents them in an expandable table. The feature supports offline recording via IndexedDB and pushes timer notifications through the service worker.

---

## Role Access

| Role | Create | View | Delete (completed) | Delete (Abandoned) |
|---|---|---|---|---|
| SuperAdmin | ✓ (all CRs) | All | ✓ | ✓ |
| Administrator | ✓ (all CRs) | All | ✓ | ✓ |
| Carer | ✓ (linked CRs) | Linked CRs | — | ✓ (linked CRs) |
| SupportWorker | ✓ (linked CRs) | Linked CRs | — | ✓ (linked CRs) |
| CareRecipient | ✓ (self) | Own | — | — |
| HealthCareProvider | — | Linked CRs | — | ✓ (linked CRs) |

---

## Backend

### Controller: `AssessmentController` — `/api/assessment`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/assessment` | Authorized | Record a new BGL assessment |
| GET | `/api/assessment` | Authorized | List assessments scoped by role |
| GET | `/api/assessment/{id}` | Authorized | Get a single assessment |
| GET | `/api/assessment/user/{userId}` | Authorized | Get assessments for a specific user |
| PATCH | `/api/assessment/{id}/complete-monitoring` | Authorized | Complete a `MonitoringInProgress` assessment after the 2-hour recheck |
| GET | `/api/assessment/in-progress` | Authorized | Get the most recent `MonitoringInProgress` assessment for the caller (returns `null` if none) |
| DELETE | `/api/assessment/{id}` | AdminOrHigher | Hard-delete a completed assessment |
| DELETE | `/api/assessment/{id}/abandoned` | Authorized (not CareRecipient) | Delete an `Abandoned` assessment |

### Assessment Endpoint Details

**POST `/api/assessment`**
- Resolves `CareRecipientId` by role: CareRecipient → self; Carer/SupportWorker → must be a linked CR; Admin/SuperAdmin → any valid user.
- Reads `termId` from JWT claim and verifies the `Cycle` exists.
- Persists the `Assessment` entity including all JSON blobs.
- When `outcome = 5` (`MonitoringInProgress`), the record is saved as an in-progress placeholder so any device can resume via `GET /in-progress`.
- Sends push notification for critical outcomes: if `outcome` is `CarerNotified` or `EmergencyRequiredCarerNotified`, dispatches an `AssessmentSeverity` push to all linked carers.

**PATCH `/api/assessment/{id}/complete-monitoring`**
- Updates a `MonitoringInProgress` record with the final recheck readings.
- Returns `409 Conflict` if the assessment `Outcome` is no longer `MonitoringInProgress` (concurrency guard — prevents double-completion from two devices).
- Sets `MonitoringRecheckBgl`, `RecheckKetoneLevel`, `AllReadingsJson`, `Notes`, `DurationSeconds`, `UpdatedAt`, and the final `Outcome`.
- Dispatches `AssessmentSeverity` push if the completed outcome is `CarerNotified` or `EmergencyRequiredCarerNotified`.

**GET `/api/assessment/in-progress`**
- Returns the caller's most recent `MonitoringInProgress` assessment (scoped by role the same way as GET list).
- Returns HTTP 200 with `null` body if no in-progress assessment exists (never 404).
- Used by `BglReadingComponent` on page load to restore a timer started on another device.

**DELETE `/api/assessment/{id}/abandoned`**
- Verifies `Outcome == Abandoned` (400 otherwise).
- Rejects `CareRecipient` role (403).
- Non-admin roles verify the CR is in their linked list before deleting.

**GET `/api/assessment`**
- Admin/SuperAdmin: returns all assessments.
- CareRecipient: returns own assessments.
- Carer/SupportWorker/HealthCareProvider: returns assessments for all linked CRs.
- Optional `?termId=N` filter.

### Key DTOs

**`CreateAssessmentDto`**
```
careRecipientId      int?       Omit for self-recording CareRecipient
initialReading       string     Numeric string, "LOW", or "HIGH"
type                 int        1=Normal, 2=Hypoglycemia, 3=Hyperglycemia
bloodTestReading     decimal?   Finger-prick confirmation value [Range(1.0, 33.3)]
recheckReading       decimal?   Post-treatment recheck value [Range(1.0, 33.3)]
treatmentCycles      int        Number of treatment cycles performed
ketoneLevel          decimal?   mmol/L ketone reading (required if BGL ≥ 15) [Range(0.0, 10.0)]
outcome              int        1=Normal, 2=Resolved, 3=CarerNotified,
                                4=EmergencyRequiredCarerNotified, 5=MonitoringInProgress, 6=Abandoned
notes                string?    Free-text notes
durationSeconds      int        Total assessment duration
allReadingsJson      string?    JSON array of every reading taken
cycleDetailsJson     string?    JSON array of each treatment cycle detail
readingDate          DateTime?  Back-dated timestamp (defaults to server now if omitted)
clientTimestamp      string?    ISO string of client-side capture time
timerScheduledAt     DateTime?  Wall-clock time the 2-hour timer was started; stored so any
                                device can calculate remaining time via GET /in-progress
```

**`CompleteMonitoringDto`**
```
monitoringRecheckBgl   decimal?   BGL taken at the post-monitoring recheck [Range(1.0, 33.3)]
recheckKetoneLevel     decimal?   Ketone taken at the post-monitoring recheck [Range(0.0, 10.0)]
outcome                int        [Required] Final outcome (1–4)
allReadingsJson        string?    Updated full readings timeline
notes                  string?    Updated notes including monitoring readings
durationSeconds        int        Total assessment duration from start
```

**`AssessmentDto`** (response)
```
id                   int
userId               int
userName             string
careRecipientId      int?
careRecipientName    string?
timestamp            DateTime
initialReading       string
type                 string     "Hypoglycemia" | "Hyperglycemia" | "Normal"
bloodTestReading     decimal?
recheckReading       decimal?
treatmentCycles      int
ketoneLevel          decimal?
outcome              string
notes                string?
durationSeconds      int
allReadingsJson      string?
cycleDetailsJson     string?
termName             string?
recheckKetoneLevel   decimal?   Ketone from the post-monitoring 2-hour recheck
monitoringRecheckBgl decimal?   BGL from the post-monitoring 2-hour recheck
timerScheduledAt     DateTime?  When the 2-hour timer was started
updatedAt            DateTime?  Last PATCH timestamp (audit trail)
```

### Model: `Assessment`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User (recorder) |
| `CareRecipientId` | int? | FK → User (subject) |
| `Timestamp` | DateTime | Server-side record time |
| `InitialReading` | string | Sensor/strip value or "LOW"/"HIGH" |
| `Type` | AssessmentType | Normal / Hypoglycemia / Hyperglycemia |
| `BloodTestReading` | decimal? | Finger-prick |
| `RecheckReading` | decimal? | Post-treatment |
| `TreatmentCycles` | int | 0–4 |
| `KetoneLevel` | decimal? | mmol/L |
| `RecheckKetoneLevel` | decimal? | Ketone from post-monitoring recheck (`decimal(5,2)`) |
| `MonitoringRecheckBgl` | decimal? | BGL from post-monitoring recheck (`decimal(5,2)`) |
| `TimerScheduledAt` | DateTime? | Wall-clock time the 2-hour timer was started |
| `UpdatedAt` | DateTime? | Set on every PATCH (audit trail) |
| `Outcome` | AssessmentOutcome | Normal=1, Resolved=2, CarerNotified=3, EmergencyRequiredCarerNotified=4, **MonitoringInProgress=5**, **Abandoned=6** |
| `Notes` | string? | |
| `TermId` | int? | FK → Cycle |
| `DurationSeconds` | int | |
| `AllReadingsJson` | string? | JSON array |
| `CycleDetailsJson` | string? | JSON array |

### BGL Classification Service

**File:** `server/src/Vitara.Api/Services/BglClassificationService.cs`

- Holds CRUD for `BglClassificationRange` and `KetoneThreshold` entities.
- Per-recipient ranges (where `CareRecipientId` is set) take **priority** over global defaults (where `CareRecipientId` is null).
- **Range matching:** `MinValue <= value < MaxValue`; null bounds are treated as unbounded.
- **Fallback:** if no range matches (e.g., unexpected reading), defaults to `Hyperglycemia / Critical`.
- `GetEffectiveRangesAsync(careRecipientId?)`: returns the resolved active set of classification ranges + ketone thresholds for a given CR (or global).
- `GetMaxBglValue(sensorValue, bloodTestValue)`: static helper — returns the higher of the two values for classification purposes.
- **Export endpoint** `GET /api/bgl-classification/effective?careRecipientId=N` — used by `BglReadingComponent` to load dynamic thresholds before starting an assessment.

### Background Service: `BgTimerHostedService`

**File:** `server/src/Vitara.Api/Services/BgTimerHostedService.cs`

Polls every 30 seconds to manage server-side VAPID push reminders for the 2-hour monitoring timer.

- **Reminder push:** When a `MonitoringInProgress` assessment's `TimerScheduledAt` is due, sends a VAPID push to all linked carers with body `"Please perform a blood glucose and ketone test."` and a deep link to `/features/assessment?careRecipientId=<id>`.
- **Stale auto-abandon:** After the configurable grace period (`Assessment.StaleMonitoringGraceHours`, default 6 hours) past `TimerScheduledAt`, automatically marks lingering `MonitoringInProgress` assessments as `Abandoned` and sets `UpdatedAt`. This prevents orphaned records if a user never completes the recheck.

---

## Frontend

### Routes

| Path | Component | Guard |
|---|---|---|
| `/assessment` | `AssessmentHistoryComponent` | `AuthGuard` |
| `/assessment/reading` | `BglReadingComponent` | `AuthGuard`, role check in component, `AssessmentCanDeactivateGuard` |

### `AssessmentHistoryComponent` — `/assessment`

**File:** `client/src/app/features/assessment/components/assessment-history.component.ts`

**On init:**
- Calls `AssessmentService.getAssessments(termId)` (termId from `AuthService.getTermId()`).
- For Admin/SuperAdmin, also loads all users via `GET /users` to populate the user filter.

**Table columns:** Expand toggle | Timestamp | User | Care Recipient | Initial Reading | Assessment Type | Outcome | Treatment Cycles | Duration | Actions (delete for admins)

**Expandable row:** Shows the full `allReadingsJson` timeline (reading type, numeric value, timestamp) and each `cycleDetailsJson` entry (cycle number, start time, treatment type, recheck value).

**Color coding:**
- `reading-normal`: 4.0–7.89 mmol/L → green
- `reading-warning`: 7.9–14.99 mmol/L → amber
- `reading-danger`: ≥15 or <4 mmol/L → red

**Duration formatting:** Converts raw `durationSeconds` to "Xm Ys" display string.

**Search:** Client-side text filter across user name, care recipient name, type, outcome, reading values, and term name.

**Excel export:** `GET /export/assessment-report?termId=N` → downloads `.xlsx` blob.

### `BglReadingComponent` — `/assessment/reading`

**File:** `client/src/app/features/assessment/components/bgl-reading.component.ts`

This is the core guided assessment workflow implemented as a state machine.

#### State Machine

```
initial
  ├── (BGL is LOW / < low threshold) → hypo-fingerprick
  │     └── hypo-treatment (15-min countdown)
  │           └── hypo-recheck
  │                 ├── (resolved, < 2 cycles) → complete
  │                 ├── (BGL=3.9 after cycle 2) → check-only cycle (up to 2 extra)
  │                 └── (not resolved, > 2 cycles) → carer-notified outcome
  │
  └── (BGL is HIGH / ≥ high threshold) → hyper-fingerprick
        └── hyper-ketone
              ├── (ketone < 0.6) → hyper-ketone-monitoring (2-hour countdown)
              │     └── hyper-ketone-recheck (captures BGL + ketone) → complete
              └── (ketone ≥ 0.6) → hyper-ketone-recheck (confirmation only)
                    ├── (confirmed critical) → emergency outcome → complete
                    └── (confirmed safe) → hyper-ketone-monitoring → hyper-ketone-recheck
```

#### Care Recipient Selection
- Auto-selects if only one CR is linked.
- Shows a dropdown for carers/admins with multiple CRs.
- Admin uses `CareRecipientService.getAllActiveCareRecipients()`; others use `getCareRecipients()`.
- On CR selection, loads effective BGL classification ranges via `BglClassificationService.getEffectiveRanges(careRecipientId)`.

#### Initial Reading
- Accepts numeric input (1.0–30.0 mmol/L) or text `LOW` / `HIGH` from CGM.
- Validates against loaded thresholds to determine the path (hypo vs hyper vs normal).
- `isKetoneOptional()`: returns `true` for T2D care recipients — the ketone step shows a "Skip" button.

#### Timer Implementation
- **Hypo treatment timer:** 15 minutes (`BglRecheckWaitMinutes` from AppConfig via `AppConfigService`).
- **Hyper monitoring timer:** 120 minutes.
- **Wall-clock accuracy:** The countdown derives `remainingSeconds` from `Math.floor((timerScheduledAtMs - Date.now()) / 1000)` on every tick, rather than simply decrementing. This prevents accumulating drift when the CPU throttles in background tabs or on locked screens.
- **Page Visibility API correction:** When the tab is hidden and then restored (`visibilitychange` event), the component immediately recalculates `remainingSeconds` from the stored `timerScheduledAtMs` wall-clock timestamp. If the timer has already expired, the component transitions straight to `hyper-ketone-recheck`.
- **Service Worker notification:** `ServiceWorkerNotificationService` schedules a local notification to alert even when the browser tab is not active.

#### Cross-Device Timer Restore
When `start2HourTimer()` is called the component first saves the assessment to the server with `outcome = 5` (`MonitoringInProgress`) and `timerScheduledAt` set to the expected expiry time. On `ngOnInit`, `checkInProgressOnServer()` calls `GET /api/assessment/in-progress`; if a record is found the component restores `initialReading`, `ketoneValue`, `timerScheduledAtMs`, and the countdown — enabling the user to seamlessly continue the assessment from a different browser or device.

#### Back-Dating
- `readingDateStr` input (datetime-local) defaults to the current time.
- Sent to the API as `readingDate` in the DTO; API uses this as the assessment `Timestamp`.

#### Submission
When the assessment completes, `saveAssessment()` takes one of two paths:

1. **PATCH path** (cross-device / normal path): if `inProgressAssessmentId` is set and the device is online, calls `PATCH /assessment/{id}/complete-monitoring` with `CompleteMonitoringDto`. A `409 Conflict` response (concurrent completion from another device) is silently accepted.
2. **POST path** (offline fallback or non-monitoring path): if the in-progress save failed (offline) or the assessment did not use the 2-hour monitoring flow, calls `POST /assessment` with the full `CreateAssessmentDto`.

`isSubmitting` is set to `true` while either request is in-flight, disabling all save-triggering buttons to prevent duplicate submissions.

#### CanDeactivate Guard
`AssessmentCanDeactivateGuard` (`client/src/app/features/assessment/guards/assessment-can-deactivate.guard.ts`) is registered on the `/assessment/reading` route. It allows navigation without a prompt only when `step === 'initial'` or `step === 'complete'`. For all other steps the user is shown a browser `confirm()` dialog noting that progress is saved on the server and can be resumed from any device.

#### Offline Fallback
- If the network request to `POST /assessment` fails with a network error or a 5xx status, `AssessmentService.createAssessment()` catches the error and enqueues the payload to `OfflineQueueService` (IndexedDB, 24-hour TTL, type `'assessment'`).
- If the in-progress save during `start2HourTimer()` fails (network error), `offlineFallback` is set to `true`. On completion, `saveAssessment()` uses the POST path and appends an offline notice to the completion message.

### Feature Service: `AssessmentService`

**File:** `client/src/app/features/assessment/services/assessment.service.ts`

| Method | HTTP | Endpoint | Notes |
|---|---|---|---|
| `createAssessment(dto)` | POST | `/assessment` | Offline fallback via `OfflineQueueService` |
| `getAssessments(termId?)` | GET | `/assessment?termId=N` | Role-scoped by server |
| `completeMonitoring(id, dto)` | PATCH | `/assessment/{id}/complete-monitoring` | 409 handled silently |
| `getInProgressAssessment()` | GET | `/assessment/in-progress` | Returns `Assessment \| null` |
| `deleteAssessment(id)` | DELETE | `/assessment/{id}` | Admin only |
| `deleteAbandonedAssessment(id)` | DELETE | `/assessment/{id}/abandoned` | Not CareRecipient |
| `getExportReport(termId?)` | GET | `/export/assessment-report` | Returns Blob |

### Shared Service: `BglClassificationService`

**File:** `client/src/app/shared/services/bgl-classification.service.ts`

| Method | HTTP | Endpoint |
|---|---|---|
| `getEffectiveRanges(careRecipientId?)` | GET | `/bgl-classification/effective` |
| `getRanges(careRecipientId?)` | GET | `/bgl-classification/ranges` |
| `createRange(dto)` | POST | `/bgl-classification/ranges` |
| `updateRange(id, dto)` | PUT | `/bgl-classification/ranges/{id}` |
| `deleteRange(id)` | DELETE | `/bgl-classification/ranges/{id}` |
| `getKetoneThresholds(careRecipientId?)` | GET | `/bgl-classification/ketone-thresholds` |
| `createKetoneThreshold(dto)` | POST | `/bgl-classification/ketone-thresholds` |
| `updateKetoneThreshold(id, dto)` | PUT | `/bgl-classification/ketone-thresholds/{id}` |
| `deleteKetoneThreshold(id)` | DELETE | `/bgl-classification/ketone-thresholds/{id}` |

---

## AppConfig Keys Relevant to This Feature

| Key | Default | Description |
|---|---|---|
| `BglRecheckWaitMinutes` | 15 | Hypo treatment cycle wait |
| `BglAdditionalWaitMinutes` | 15 | Additional check-only cycle wait |
| `HyperGlucoseReminderIntervalMinutes` | 120 | Hyper monitoring timer |
| `HypoMaxTreatmentCycles` | 2 | Max full treatment cycles |
| `HypoMaxCheckOnlyCycles` | 2 | Max extra check-only cycles after cycle 2 |
| `Assessment.StaleMonitoringGraceHours` | 6 | Hours after the 2-hour timer fires before an unfinished `MonitoringInProgress` assessment is automatically marked `Abandoned` by `BgTimerHostedService` |

---

## Push Notifications Sent

| Trigger | Notification Type | Recipients |
|---|---|---|
| Outcome = CarerNotified | `AssessmentSeverity` (3) | All linked carers |
| Outcome = EmergencyRequiredCarerNotified | `AssessmentSeverity` (3) | All linked carers |
| 2-hour timer expires (server-sent VAPID) | `BgTimerReminder` (6) | Linked carers of the CR |
| Timer expires (in-browser) | Local SW notification | Device only (no server push) |

---

## End-to-End Data Flow

```
Carer/CareRecipient                Angular                     API                   DB
  |                                |                            |                     |
  | Select care recipient          |                            |                     |
  |                                |-- GET /bgl-classification/effective? -->          |
  |                                |<-- classification ranges --|                     |
  |                                |-- GET /assessment/in-progress ---------->|        |
  |                                |<-- null (no active session)|                     |
  |                                |                            |                     |
  | Enter initial reading (e.g. 16)|                            |                     |
  |                                | state → hyper-fingerprick  |                     |
  | Enter blood test (16.4)        | state → hyper-ketone       |                     |
  | Enter ketone (0.3)             | state → hyper-ketone-monitoring                  |
  |                                |                            |                     |
  | Clicks start 2-hour timer      |                            |                     |
  |                                |-- POST /assessment (outcome=5, timerScheduledAt) |
  |                                |                            |-- INSERT (InProgress)->|
  |                                |<-- { id: 42 } -------------|                     |
  |                                | inProgressAssessmentId=42  |                     |
  |                                | 2-hour countdown begins    |                     |
  |                                |                            |                     |
  | Timer expires / reopens app    |                            |                     |
  | (same or different device)     | checkInProgressOnServer()  |                     |
  |                                |-- GET /assessment/in-progress ----------->|       |
  |                                |<-- Assessment { id:42, timerScheduledAt } |       |
  |                                | restores countdown         |                     |
  |                                |                            |                     |
  | Enter BGL recheck (14.1)       |                            |                     |
  | Enter ketone recheck (0.2)     | state → complete           |                     |
  |                                |-- PATCH /assessment/42/complete-monitoring ->|   |
  |                                |                            |-- UPDATE Outcome=2 ->|
  |                                |                            |-- push if critical   |
  |                                |<-- 200 OK (or 409 if already completed)          |
  |<-- navigate /assessment        |                            |                     |
```
