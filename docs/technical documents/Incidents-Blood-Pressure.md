# Blood Pressure Incidents

## Overview

The Blood Pressure Incidents feature provides a quick-capture form for recording hypertension-related incidents for care recipients diagnosed with High Blood Pressure (HBP). Unlike the multi-reading BP Monitoring session, a BP Incident is a single observed event: one systolic/diastolic pair, optional pulse rate, medication status, and supervision details. The server auto-classifies the reading using the AHA category system and fires push alerts for serious classifications. Only care recipients with an active HBP condition may have BP incidents recorded against them.

---

## Role Access

| Role | Create | View | Edit | Delete | Carer Comment |
|---|---|---|---|---|---|
| SuperAdmin | ✓ (all CRs) | All | ✓ | ✓ | ✓ |
| Administrator | ✓ (all CRs) | All | ✓ | ✓ | ✓ |
| Carer | ✓ (linked HBP CRs) | Linked CRs | ✓ | — | ✓ |
| SupportWorker | ✓ (linked HBP CRs) | Linked CRs | ✓ | — | — |
| CareRecipient | — | Own | — | — | — |
| HealthCareProvider | ✓ (linked HBP CRs) | Linked CRs | ✓ | — | — |

---

## Backend

### Controller: `BpIncidentController` — `/api/bp-incident`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| POST | `/api/bp-incident/entries` | SupportOrHigher | Create a BP incident |
| GET | `/api/bp-incident/entries` | Authorized | List BP incidents (role-scoped) |
| GET | `/api/bp-incident/entries/{id}` | Anonymous | Get single BP incident |
| PUT | `/api/bp-incident/entries/{id}` | SupportOrHigher | Update a BP incident |
| DELETE | `/api/bp-incident/entries/{id}` | AdminOrHigher | Hard-delete |
| PATCH | `/api/bp-incident/entries/{id}/carer-comment` | Authorized | Carer adds/updates comment |
| GET | `/api/bp-incident/weekly` | Anonymous | BP incidents from the last 7 days |

### Business Logic — POST `/api/bp-incident/entries`

1. **HBP Guard:** `BpIncidentService.CareRecipientHasHbpAsync(careRecipientId)` checks that the target CR has an active HBP `UserCondition`. Returns 400 if not.
2. **Auto-classification:**
   - Calls `BpClassificationService.ClassifyAsync(systolic, diastolic)` → `Category` + `Severity`.
   - If `pulseRate` is provided, calls `PulseRateClassificationService.ClassifyAsync(pulseRate)` → `PulseRateCategory` + `PulseRateSeverity`.
3. **Push notifications:** Fires `BpIncidentSeverity` (type 12) push to all linked carers for:
   - `Category == Hypotension`
   - `Category == HypertensionStage2`
   - `Category == HypertensiveCrisis`
4. Sets `TermId` from the default active cycle.

### Business Logic — PUT `/api/bp-incident/entries/{id}`

- Re-runs full classification with updated systolic/diastolic values.
- Re-evaluates pulse rate category if provided.
- Validates carer comment role restriction (same as diabetes incidents).

### Key DTOs

**`CreateBpIncidentRequest`**
```
careRecipientId      int        Must have active HBP condition
termId               int        Active cycle
systolic             int        40–300 mmHg
diastolic            int        20–200 mmHg
pulseRate            int?       Optional
medicationAdministered  bool    Whether BP medication was taken
medicationName       string?    Which medication (free text)
requiredSupervision  bool
supervisionDuration  int?       Minutes
supportComment       string?
incidentResolvedAt   DateTime?
```

**`BpIncidentResponse`**
```
id                   int
userId               int
userName             string
careRecipientId      int
careRecipientName    string
termId               int
systolic             int
diastolic            int
pulseRate            int?
pulseRateCategory    string?
pulseRateSeverity    string?
category             string
severity             string
medicationAdministered  bool
medicationName       string?
requiredSupervision  bool
supervisionDuration  int?
supportComment       string?
carerComment         string?
incidentResolvedAt   DateTime?
createdAt            DateTime
```

**`UpdateBpCarerCommentRequest`**
```
carerComment    string
```

### Model: `BpIncident`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `UserId` | int | FK → User (recorder) |
| `CareRecipientId` | int | FK → User |
| `TermId` | int | FK → Cycle |
| `Systolic` | int | |
| `Diastolic` | int | |
| `PulseRate` | int? | |
| `PulseRateCategory` | string? | |
| `PulseRateSeverity` | string? | |
| `Category` | BpCategory | Hypotension=1 … HypertensiveCrisis=6 |
| `Severity` | Severity | Normal=1 … Critical=5 |
| `MedicationAdministered` | bool | |
| `MedicationName` | string? | |
| `RequiredSupervision` | bool | |
| `SupervisionDuration` | int? | Minutes |
| `SupportComment` | string? | |
| `CarerComment` | string? | |
| `IncidentResolvedAt` | DateTime? | |
| `CreatedAt` | DateTime | |
| `UpdatedAt` | DateTime | |

### `BpIncidentService`

**File:** `server/src/Vitara.Api/Services/BpIncidentService.cs`

**Dependencies:** `ApplicationDbContext`

Key operations:
- Full CRUD on `BpIncidents`.
- `CareRecipientHasHbpAsync(careRecipientId)` — queries `UserConditions` for a row with `ConditionId` matching HBP and `UserId == careRecipientId`.
- Same term/link validation pattern as `IncidentService`.
- Reads include navigation properties: `User`, `CareRecipient`, `Term`.

---

## Frontend

All BP incident UI lives inside the shared `/incidents` route within the `IncidentComponent`.

### `IncidentComponent` (Blood Pressure tab) — `/incidents`

**File:** `client/src/app/features/incident/components/incident.component.ts`

**Tabs:** All | Diabetes | **Blood Pressure** ← (this document)

#### BP Incident Table
Columns: Date | Captured By | Care Recipient | Systolic/Diastolic | Severity | Medication | Supervision | Actions

Category badge colours: Hypotension → blue; Normal → green; Elevated → yellow; Stage 1 → orange; Stage 2 → red; Crisis → dark red.

#### BP Incident Form

Rendered via `BpIncidentFormComponent` embedded in the `IncidentComponent` template.

**`BpIncidentFormComponent`**

**File:** `client/src/app/features/incident/components/bp-incident-form/bp-incident-form.component.ts`

Fields:
- **Care recipient:** Auto-populated from the `IncidentComponent` CR selection; available CRs are filtered to those with `hasHbp = true` in the carer's linked CR list.
- **Systolic:** numeric, 40–300 (required).
- **Diastolic:** numeric, 20–200 (required).
- **Pulse rate:** numeric, optional.
- **AHA category preview:** computed client-side using loaded classification ranges as values change, displayed as a coloured badge (e.g., "Stage 2 Hypertension — Severe").
- **Medication administered toggle:** `true` reveals a free-text medication name field.
- **Supervision required toggle:** `true` reveals a duration field.
- **Support comment:** optional free text.
- Emits `submitted` or `cancelled` events to parent.

In **edit mode**: form pre-fills from the existing `BpIncidentResponse` and calls `BpIncidentService.updateIncident(id, dto)` on submit.

In **create mode**: calls `BpIncidentService.createIncident(dto)`.

Offline fallback applies for both create and update paths.

#### Carer Comment Panel (BP Incidents)
Same pattern as diabetes incidents — Carer/Admin roles see a comment area per row, calling `PATCH /bp-incident/entries/{id}/carer-comment`.

### Feature Service: `BpIncidentService`

**File:** `client/src/app/features/incident/services/bp-incident.service.ts`

| Method | HTTP | Endpoint | Notes |
|---|---|---|---|
| `createIncident(dto)` | POST | `/bp-incident/entries` | Offline fallback |
| `getIncidents(termId?)` | GET | `/bp-incident/entries?termId=N` | |
| `updateIncident(id, dto)` | PUT | `/bp-incident/entries/{id}` | Offline fallback |
| `deleteIncident(id)` | DELETE | `/bp-incident/entries/{id}` | |
| `addCarerComment(id, comment)` | PATCH | `/bp-incident/entries/{id}/carer-comment` | |

---

## Smart Form Routing (Shared)

The `IncidentComponent` determines which form to show when "Add Incident" is clicked:

```typescript
if (selectedCR.hasHbp && selectedCR.hasDiabetes) {
  // Show IncidentTypePickerComponent
} else if (selectedCR.hasHbp) {
  // Directly show BpIncidentFormComponent
} else {
  // Directly show diabetes incident inline form
}
```

For Admin/SuperAdmin users who can select any CR, the type picker is always shown if the CR has both conditions.

---

## Push Notifications Sent

| Trigger | Notification Type | Recipients |
|---|---|---|
| Category = Hypotension | `BpIncidentSeverity` (12) | All linked carers |
| Category = HypertensionStage2 | `BpIncidentSeverity` (12) | All linked carers |
| Category = HypertensiveCrisis | `BpIncidentSeverity` (12) | All linked carers |

---

## Validation Rules

| Rule | Details |
|---|---|
| HBP guard | `CareRecipientHasHbpAsync` must return true; 400 otherwise |
| Systolic range | 40–300 mmHg (client + server) |
| Diastolic range | 20–200 mmHg (client + server) |
| Medication name | Required when `medicationAdministered = true` |
| Supervision duration | Required when `requiredSupervision = true` |

---

## End-to-End Data Flow

```
Carer                       Angular                        API                   DB
  |                         |                               |                     |
  | Click "Add Incident"    |                               |                     |
  | (CR has HBP only)       |                               |                     |
  |                         | Show BP incident form directly|                     |
  |                         |                               |                     |
  | Enter: sys=185, dia=115 |                               |                     |
  | Preview: Crisis         |                               |                     |
  | Medication: yes         |                               |                     |
  |                         |                               |                     |
  | Submit                  |-- POST /bp-incident/entries ->|                     |
  |                         |                               |-- HBP guard check   |
  |                         |                               |-- classify BP        |
  |                         |                               |-- INSERT BpIncident->|
  |                         |                               |-- push to carers     |
  |                         |<-- BpIncidentResponse --------|                     |
  | Row appears in BP tab   |                               |                     |
  | with Crisis badge       |                               |                     |
```
