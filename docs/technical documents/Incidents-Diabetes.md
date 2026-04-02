# Diabetes Incidents

## Overview

The Diabetes Incidents feature provides a quick-entry form for recording BGL-related incidents — distinct from the full guided BGL Assessment workflow. An incident captures a single moment-in-time observation: a sensor or blood-test reading, the classificaton outcome (Normal, Hypoglycemia, Hyperglycemia), severity, any insulin administered, ketone reading, and whether supervision was required. Carer staff can also annotate incidents with a carer comment after the fact. High and Critical severity incidents automatically trigger push notifications to linked carers.

---

## Role Access

| Role | Create | View | Edit | Delete | Carer Comment |
|---|---|---|---|---|---|
| SuperAdmin | ✓ | All | ✓ | ✓ | ✓ |
| Administrator | ✓ | All | ✓ | ✓ | ✓ |
| Carer | ✓ (linked CRs) | Linked CRs | ✓ | — | ✓ |
| SupportWorker | ✓ (linked CRs) | Linked CRs | ✓ | — | — |
| CareRecipient | — | Own | — | — | — |
| HealthCareProvider | ✓ (linked CRs) | Linked CRs | ✓ | — | — |

---

## Backend

### Controller: `IncidentController` — `/api/incident`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| POST | `/api/incident/entries` | SupportOrHigher | Create a BGL incident |
| GET | `/api/incident/entries` | Authorized | List incidents (role-scoped, optional filters) |
| GET | `/api/incident/entries/{id}` | Anonymous | Get a single incident |
| PUT | `/api/incident/entries/{id}` | SupportOrHigher | Update an incident |
| DELETE | `/api/incident/entries/{id}` | AdminOrHigher | Hard-delete an incident |
| PATCH | `/api/incident/entries/{id}/carer-comment` | Authorized | Carer adds or updates a comment |
| GET | `/api/incident/weekly` | Anonymous | All incidents from the last 7 days |

### Business Logic — POST `/api/incident/entries`

1. **Link validation:** For Carer, SupportWorker, and HealthCareProvider roles, verifies the `CareRecipientId` is in their linked CR list.
2. **Auto-classification:** Calls `BglClassificationService.ClassifyAsync(sensorValue, bloodTestValue)` using the CR's effective ranges → sets `State` (Normal/Hypoglycemia/Hyperglycemia) and `Severity` (Normal/Minor/Moderate/Severe/Critical).
3. **Ketone validation:** If the higher of `sensorValue` or `bloodTestValue` is ≥ 15 mmol/L, a `ketoneReading` must be provided (400 if missing).
4. **Push notifications:** If `Severity` is High or Critical (`>= 4`), dispatches an `IncidentSeverity` push to all linked carers of the care recipient.
5. Sets `TermId` from the JWT's `termId` claim.

### Business Logic — PUT `/api/incident/entries/{id}`

- Re-runs full classification with the updated values.
- Validates carer comment restriction: only `Carer` and `Administrator`/`SuperAdmin` roles can write `CarerComment`; `SupportWorker` cannot.

### Business Logic — PATCH carer-comment

- Restricted to `Carer`, `Administrator`, and `SuperAdmin` roles (checked in service).
- Allows adding or overwriting the `CarerComment` field on an existing incident.

### Key DTOs

**`CreateIncidentRequest`**
```
sensorValue          decimal?   mmol/L, 0.1–30.0 (at least one of sensor or blood is required)
bloodTestValue       decimal?   mmol/L
supportComment       string?    Note from SupportWorker/Carer at time of incident
carerComment         string?    Secondary annotation from Carer
termId               int        Current active cycle
state                int        1=Normal, 2=Hypoglycemia, 3=Hyperglycemia
severity             int?       If omitted, auto-classified from BGL values
insulinAdministered  bool       Whether insulin was given
insulinUnits         decimal?   Required if insulinAdministered = true
requiredSupervision  bool       Whether supervision was needed
supervisionDuration  int?       Minutes of supervision
ketoneReading        decimal?   mmol/L; required if BGL ≥ 15
careRecipientId      int        Target care recipient
incidentResolvedAt   DateTime?  When the incident was resolved
```

**`IncidentResponse`**
```
id                   int
userId               int
userName             string
careRecipientId      int
careRecipientName    string
termId               int
termName             string
sensorValue          decimal?
bloodTestValue       decimal?
state                string
severity             string?
insulinAdministered  bool
insulinUnits         decimal?
requiredSupervision  bool
supervisionDuration  int?
ketoneReading        decimal?
supportComment       string?
carerComment         string?
dateResolved         DateTime
incidentResolvedAt   DateTime?
```

**`UpdateCarerCommentRequest`**
```
carerComment    string    The comment text
```

### Model: `Incident`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User (recorder) |
| `CareRecipientId` | int | FK → User |
| `TermId` | int | FK → Cycle |
| `SensorValue` | decimal? | 5,1 precision |
| `BloodTestValue` | decimal? | 5,1 precision |
| `DateResolved` | DateTime | Server-set timestamp |
| `SupportComment` | string? | |
| `CarerComment` | string? | |
| `State` | BglState | Normal / Hypoglycemia / Hyperglycemia |
| `Severity` | Severity? | Normal=1 … Critical=5 |
| `InsulinAdministered` | bool | |
| `InsulinUnits` | decimal? | |
| `RequiredSupervision` | bool | |
| `SupervisionDuration` | int? | Minutes |
| `KetoneReading` | decimal? | 3,1 precision |
| `IncidentResolvedAt` | DateTime? | |
| `CreatedAt` | DateTime | |
| `UpdatedAt` | DateTime | |

### `IncidentService`

**File:** `server/src/Vitara.Api/Services/IncidentService.cs`

**Dependencies:** `ApplicationDbContext`

Key methods:
- `GetIncidentsByTermAsync(termId)` — Admin/SuperAdmin scope.
- `GetIncidentsByUserAndTermAsync(userId, termId)` — CareRecipient scope.
- `GetIncidentsByCareRecipientIdsAsync(crIds, termId?)` — Carer/SupportWorker scope.
- `GetWeeklyIncidentsAsync()` — returns incidents from last 7 days.
- All reads include navigation properties: `User`, `Term`, `CareRecipient`.

---

## Frontend

### Route

| Path | Component | Module |
|---|---|---|
| `/incidents` | `IncidentComponent` | `IncidentModule` |

The Incidents page is a tabbed view hosting both diabetes incidents (this document) and BP incidents (see [Incidents-Blood-Pressure.md](Incidents-Blood-Pressure.md)).

### `IncidentComponent` (Diabetes tab) — `/incidents`

**File:** `client/src/app/features/incident/components/incident.component.ts`

#### Tab Structure
- **All** — shows both diabetes and BP incidents interleaved.
- **Diabetes** — diabetes incidents only (this section).
- **Blood Pressure** — BP incidents only.

#### Smart Form Routing (Incident Type Picker)
When the user clicks "Add Incident":
- If the CR has **only diabetes** conditions → diabetes incident form shown directly.
- If the CR has **only HBP** → BP incident form shown directly.
- If the CR has **both conditions** (or is Admin viewing any CR) → `IncidentTypePickerComponent` modal shown first.

**`IncidentTypePickerComponent`:** Dialog-style overlay with two large buttons ("Diabetes" and "Blood Pressure"). Emits `typeSelected('diabetes' | 'bp')` or `pickCancelled` to the parent.

#### Diabetes Incident Table
Columns: Date Resolved | Time | Captured By | Care Recipient | Sensor Reading | Blood Test | State | Severity | Insulin | Supervision | Actions

**State colour coding:** Normal → green badge; Hypoglycemia → blue/amber; Hyperglycemia → red.

#### Inline Add/Edit Form
Toggled by clicking "Add" or the edit icon on a row. Fields:
- Care recipient selector (hidden for single-CR carers).
- Sensor value (optional, mmol/L).
- Blood test value (optional, mmol/L; at least one of sensor/blood required).
- Support comment (free text).
- Insulin administered toggle + units field (shown when toggled on).
- Supervision required toggle + duration in minutes.
- Ketone reading — **dynamically required** when either BGL value ≥ 15 mmol/L (validator updates on keystroke).
- Resolved at datetime picker.

**Auto-comment logic:** If the sensor reading differs significantly from the blood test reading (potential false positive), the form pre-fills an auto-comment suggesting a discrepancy.

#### Carer Comment Panel
Separate expandable section on each incident row for Carer and Admin roles. Displays existing `CarerComment` and shows a text area + save button for adding/updating.

#### Delete
Uses `DialogService.confirmDelete()`. Available to Admin/SuperAdmin only.

#### Data Loading
`forkJoin([IncidentService.getIncidents(), BpIncidentService.getIncidents()])` — both loaded in parallel on component init for the combined "All" tab.

#### Excel Export
- **Download:** `GET /export/incident-report` → `.xlsx` blob.
- **Google Drive upload:** calls `POST /export/upload-incident-to-drive/{careRecipientId}`; availability gated on `GoogleDriveService.getCareRecipientDriveStatus(cr.id)`.

#### Offline Fallback
`IncidentService.createIncident()` and `updateIncident()` catch network/5xx errors and enqueue to `OfflineQueueService` with type `'incident'`.

### Feature Service: `IncidentService`

**File:** `client/src/app/features/incident/services/incident.service.ts`

| Method | HTTP | Endpoint | Notes |
|---|---|---|---|
| `createIncident(dto)` | POST | `/incident/entries` | Offline fallback |
| `getIncidents(termId?)` | GET | `/incident/entries?termId=N` | |
| `updateIncident(id, dto)` | PUT | `/incident/entries/{id}` | Offline fallback |
| `deleteIncident(id)` | DELETE | `/incident/entries/{id}` | |
| `addCarerComment(id, comment)` | PATCH | `/incident/entries/{id}/carer-comment` | |
| `getExportReport()` | GET | `/export/incident-report` | Returns Blob |
| `uploadToDrive(careRecipientId)` | POST | `/export/upload-incident-to-drive/{id}` | |

---

## Push Notifications Sent

| Trigger | Notification Type | Recipients |
|---|---|---|
| Severity is High (4) or Critical (5) | `IncidentSeverity` (2) | All linked carers |
| Ketone threshold ≥ 0.6 mmol/L (manually triggered) | `General` (1) | Linked carers (via `/notification/ketone-alert`) |

The ketone alert endpoint (`POST /api/notification/ketone-alert`) is called from the `IncidentService` on the client when the ketone reading entered is ≥ 0.6, triggering a manual server-side push dispatch to all linked carers.

---

## Dynamic Validation Rules (Frontend)

| Condition | Validation Effect |
|---|---|
| BGL sensor or blood test ≥ 15 mmol/L | `ketoneReading` becomes required |
| `insulinAdministered = true` | `insulinUnits` becomes required |
| `requiredSupervision = true` | `supervisionDuration` becomes required |
| Neither sensor nor blood test provided | Form invalid (at least one must have a value) |

---

## End-to-End Data Flow

```
Support Worker                  Angular                       API                  DB
  |                             |                              |                    |
  | Clicks "Add Incident"       |                              |                    |
  |                             | show form (or type picker)   |                    |
  |                             |                              |                    |
  | Fill: sensor=16.2, ketone=0.8|                             |                    |
  | Insulin: yes, 4 units       |                              |                    |
  |                             |                              |                    |
  | Submit                      |-- POST /incident/entries --->|                    |
  |                             |                              |-- link check        |
  |                             |                              |-- classify BGL      |
  |                             |                              |-- ketone required?  |
  |                             |                              |-- INSERT Incident ->|
  |                             |                              |-- push to carers    |
  |                             |<-- IncidentResponse ---------|                    |
  | Incident row appears in     |                              |                    |
  | table, red High badge       |                              |                    |
  |                             |                              |                    |
  | Later: carer adds comment   |-- PATCH .../carer-comment -->|                    |
  |                             |<-- 200 OK ------------------|                    |
```
