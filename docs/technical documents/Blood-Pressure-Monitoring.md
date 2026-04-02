# Blood Pressure Monitoring

## Overview

The Blood Pressure Monitoring feature enables recording of multi-reading blood pressure sessions and reviewing historical session data. A session consists of two or more individual readings taken with a rest timer between each, after which the application computes averages, classifies the result using AHA-aligned categories (Hypotension through Hypertensive Crisis), and captures the active medication status. Sessions are stored with full reading arrays as JSON and weekly aggregate averages are available for trend analysis. The feature supports offline recording and sends push notifications when a critical classification is detected.

---

## Role Access

| Role | Create Session | View | Delete |
|---|---|---|---|
| SuperAdmin | ✓ (all CRs) | All | ✓ |
| Administrator | ✓ (all CRs) | All | ✓ |
| Carer | ✓ (linked CRs) | Linked CRs | ✓ |
| SupportWorker | ✓ (linked CRs) | Linked CRs | — |
| CareRecipient | ✓ (self) | Own | — |
| HealthCareProvider | — (read only) | Linked CRs | — |

---

## Backend

### Controller: `BloodPressureController` — `/api/blood-pressure`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/blood-pressure` | Authorized | Create a multi-reading BP session |
| GET | `/api/blood-pressure` | Authorized | List sessions (role-scoped) |
| GET | `/api/blood-pressure/{id}` | Authorized | Get a single session |
| DELETE | `/api/blood-pressure/{id}` | AdminOrHigher | Delete a session |

**Additional routes under `BloodPressureController`:**

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/api/blood-pressure/weekly-averages` | Authorized | 7-day rolling averages per care recipient |
| GET | `/api/blood-pressure/pulse-rate-ranges` | Authorized | Pulse rate classification ranges |
| GET | `/api/blood-pressure/bp-classification-ranges` | Authorized | BP classification ranges |
| POST | `/api/blood-pressure/bp-classification-ranges` | AdminOrHigher | Create a classification range |
| PUT | `/api/blood-pressure/bp-classification-ranges/{id}` | AdminOrHigher | Update range |
| DELETE | `/api/blood-pressure/bp-classification-ranges/{id}` | AdminOrHigher | Delete range (seed rows protected) |

### POST `/api/blood-pressure` — Session Creation Detail

1. Resolves `CareRecipientId` by role (same pattern as Assessment).
2. Validates readings: each must have `Systolic` and `Diastolic`; `PulseRate` is optional.
3. Computes **averages** across all readings: `AverageSystolic`, `AverageDiastolic`, `AveragePulseRate?`.
4. Calls `BpClassificationService.ClassifyAsync(avgSys, avgDia)` → `Category` + `Severity`.
5. Calls `PulseRateClassificationService.ClassifyAsync(avgPulse?)` → `PulseRateCategory` + `PulseRateSeverity`.
6. Captures **active medication snapshot**: queries the CR's active `BpMedication` record and serialises it to `MedicationSnapshot` JSON (name + dose + frequency at time of session).
7. Serialises individual readings array to `ReadingsJson`.
8. Sets `TermId` from the default cycle.
9. On `Severity == Critical`: sends `BloodPressureAlert` (type 9) push to all linked carers.
10. Returns full session DTO including computed averages and classifications.

### Key DTOs

**`CreateBpSessionDto`**
```
readings[]           object[]   Required, min 1
  systolic           int        40–300 mmHg
  diastolic          int        20–200 mmHg
  pulseRate          int?       Optional bpm
careRecipientId      int?       Omit for self-recording CareRecipient
readingDate          DateTime?  Back-dated session datetime
clientTimestamp      string?    ISO client capture time
notes                string?    Free-text
medicationTaken      bool?      Whether medication was taken
medicationStatus     int?       0=Unknown, 1=Before, 2=After, 3=AboutTo, 4=NoMedicationConsumed
```

**`BpSessionDto`** (response — abbreviated)
```
id                   int
userId               int
userName             string
careRecipientName    string?
timestamp            DateTime
averageSystolic      decimal
averageDiastolic     decimal
averagePulseRate     decimal?
category             string     "Hypotension" | "Normal" | "Elevated" | "HypertensionStage1" | "HypertensionStage2" | "HypertensiveCrisis"
severity             string
pulseRateCategory    string?
pulseRateSeverity    string?
readingsJson         string     JSON array of individual readings
medicationSnapshot   string?    JSON object { name, dose, frequency }
medicationTaken      bool?
medicationStatus     string?
termName             string?
```

### Model: `BloodPressureSession`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User (recorder) |
| `CareRecipientId` | int? | FK → User (subject) |
| `TermId` | int? | FK → Cycle |
| `Timestamp` | DateTime | Server record time |
| `ClientTimestamp` | string? | ISO client capture time |
| `AverageSystolic` | decimal | |
| `AverageDiastolic` | decimal | |
| `AveragePulseRate` | decimal? | |
| `Category` | BpCategory enum | Hypotension=1 … HypertensiveCrisis=6 |
| `Severity` | Severity enum | Normal=1 … Critical=5 |
| `PulseRateCategory` | string? | |
| `PulseRateSeverity` | string? | |
| `ReadingsJson` | string | JSON array |
| `Notes` | string? | |
| `MedicationSnapshot` | string? | JSON object |
| `MedicationTaken` | bool? | |
| `MedicationStatus` | MedicationStatus enum | |

### BpClassificationService

**File:** `server/src/Vitara.Api/Services/BpClassificationService.cs`

- Per-recipient ranges take priority over global seeds.
- Ranges sorted by `SortOrder ASC`; first match wins.
- **OR logic** (`IsOrLogic = true`): match if systolic OR diastolic meets the criterion.
- **AND logic** (`IsOrLogic = false`): match only if both systolic AND diastolic are in range.
- Fallback: `HypertensiveCrisis / Critical`.
- 6 global default seed ranges cannot be deleted.

### PulseRateClassificationService

Same per-recipient/global override pattern. Range matching by `MinBpm <= value < MaxBpm`. 4 global defaults cannot be deleted.

---

## Frontend

### Routes

| Path | Component | Notes |
|---|---|---|
| `/blood-pressure` | `BpHistoryComponent` | All roles |
| `/blood-pressure/entry` | `BpEntryComponent` | All except HealthCareProvider (route data guard) |

### `BpHistoryComponent` — `/blood-pressure`

**File:** `client/src/app/features/blood-pressure/components/bp-history.component.ts`

**On init:**
- Calls `BloodPressureService.getSessions(termId)` and `BloodPressureService.getWeeklyAverages()`.

**Table columns:** Expand | Timestamp | Care Recipient | Recorded By | Avg Systolic | Category | Severity | Actions

**Expandable row:** Renders individual readings from `readingsJson` (supports both PascalCase and camelCase keys for backward compatibility), shows medication snapshot (name + dose + frequency), and medication timing status (Before/After/About To/No Medication etc.)

**Delete:** Available for Administrator, SuperAdmin, and Carer roles. Uses `DialogService.confirmDelete()` before calling `BloodPressureService.deleteSession(id)`.

**HealthCareProvider:** "Add Reading" button is hidden; read-only view only.

**Colour-coded categories:** Each `category` string maps to a CSS class for visual severity indication.

### `BpEntryComponent` — `/blood-pressure/entry`

**File:** `client/src/app/features/blood-pressure/components/bp-entry.component.ts`

This component implements a multi-step wizard.

#### Configuration Loading
On init:
- Loads `BpReadingCount` from `AppConfigService.getByKey('BpReadingCount')` — determines how many readings to collect (default 2).
- Loads pulse rate ranges via `BloodPressureService.getPulseRateRanges()`.

#### Step Sequence
```
[care-recipient] → reading-1 → timer-1 → reading-2 → [timer-2 → reading-3 → …] → summary → complete
```
The `care-recipient` step is skipped if the user has only one linked CR or is a CareRecipient self-recording.

#### Reading Step
- `FormArray` with one entry per configured reading count.
- Each entry: `systolic` (40–300, required), `diastolic` (20–200, required), `pulseRate` (optional, 20–300).
- **AHA category preview:** Computed client-side after each keypress using the classification ranges loaded from `BloodPressureService.getBpClassificationRanges()`. Displays e.g. "Stage 2 Hypertension" in real time below the inputs.

#### Timer Step (2-minute rest)
- 2-minute countdown (120 seconds) between consecutive readings.
- **Page Visibility API correction:** On `visibilitychange`, recalculates elapsed seconds from a stored `timerStartTime` to correct for browser throttling.
- User can click **"Skip Timer"** — the reading is flagged `timerSkipped: true` in the readings array.
- `sessionDateUserEdited` flag: if the user has manually changed the back-date field, the timer completion callback does not overwrite it.

#### Summary Step
- Computes and displays averages: `avgSystolic`, `avgDiastolic`, `avgPulseRate?`.
- Resolves the overall AHA category + severity for the averaged values.
- Loads the CR's active BP medication via `UserConditionsService.getActiveBpMedication(careRecipientId?)`.
- Medication status selector: Unknown / Before / After / AboutTo / NoMedicationConsumed.
- "Session date" datetime-local input for back-dating.

#### Offline Fallback
`BloodPressureService.createSession()` catches network/5xx errors and enqueues the payload to `OfflineQueueService` with type `'blood-pressure'`.

#### Submission
Calls `BloodPressureService.createSession(dto)` → `POST /blood-pressure` with the full `CreateBpSessionDto` including the readings array, medication status, and optional back-dated timestamp.

### Feature Service: `BloodPressureService`

**File:** `client/src/app/features/blood-pressure/services/blood-pressure.service.ts`

| Method | HTTP | Endpoint | Notes |
|---|---|---|---|
| `createSession(dto)` | POST | `/blood-pressure` | Offline fallback |
| `getSessions(termId?)` | GET | `/blood-pressure?termId=N` | |
| `deleteSession(id)` | DELETE | `/blood-pressure/{id}` | |
| `getWeeklyAverages()` | GET | `/blood-pressure/weekly-averages` | |
| `getPulseRateRanges(careRecipientId?)` | GET | `/blood-pressure/pulse-rate-ranges` | |
| `getBpClassificationRanges(careRecipientId?)` | GET | `/blood-pressure/bp-classification-ranges` | |
| `createBpClassificationRange(dto)` | POST | `/blood-pressure/bp-classification-ranges` | |
| `updateBpClassificationRange(id, dto)` | PUT | `/blood-pressure/bp-classification-ranges/{id}` | |
| `deleteBpClassificationRange(id)` | DELETE | `/blood-pressure/bp-classification-ranges/{id}` | |

---

## AppConfig Keys Relevant to This Feature

| Key | Default | Description |
|---|---|---|
| `BpReadingCount` | 2 | Number of readings per session |
| `HighBpRecheckWaitMinutes` | 2 | Rest timer duration between readings (minutes) |
| `HighBpRequiredChecks` | 2 | Minimum readings before summary is shown |

---

## Push Notifications Sent

| Trigger | Notification Type | Recipients |
|---|---|---|
| Session severity = Critical | `BloodPressureAlert` (9) | All linked carers of the care recipient |

---

## BP Categories (AHA-aligned)

| Category | Systolic | Diastolic |
|---|---|---|
| Hypotension | < 90 | OR < 60 |
| Normal | 90–119 | AND 60–79 |
| Elevated | 120–129 | AND < 80 |
| Hypertension Stage 1 | 130–139 | OR 80–89 |
| Hypertension Stage 2 | 140–179 | OR 90–119 |
| Hypertensive Crisis | ≥ 180 | OR ≥ 120 |

*Classification ranges are configurable per care recipient or globally via Management → BP Classification.*

---

## End-to-End Data Flow

```
Carer/CareRecipient            Angular                      API                   DB
  |                            |                             |                     |
  | Select care recipient      |                             |                     |
  |                            |-- GET /bp-classification-ranges? -->               |
  |                            |-- GET /pulse-rate-ranges? --|                     |
  |                            |<-- ranges loaded -----------|                     |
  |                            |                             |                     |
  | Enter reading 1 (e.g. 138/90)|                           |                     |
  | Preview: Stage 1 shown     |                             |                     |
  | Wait 2-min timer           | (timer, may skip)           |                     |
  | Enter reading 2 (e.g. 142/92)|                           |                     |
  |                            |                             |                     |
  | Summary: avg=140/91, Stage 2|                            |                     |
  | Load active medication      |-- GET active BP med ------->|                    |
  | Select: "Taken Before"     |                             |                     |
  |                            |                             |                     |
  | Submit                     |-- POST /blood-pressure ---->|                     |
  |                            |                             |-- classify averages  |
  |                            |                             |-- INSERT session --->|
  |                            |                             | if Critical: push    |
  |                            |<-- BpSessionDto ------------|                     |
  |<-- navigate /blood-pressure|                             |                     |
```
