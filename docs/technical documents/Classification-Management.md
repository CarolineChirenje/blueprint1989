# Classification Management

## Overview

Classification Management defines the medical threshold ranges used to categorise clinical readings into actionable severity levels. Four classification systems exist:

1. **BGL (Blood Glucose Level) Ranges** — Hypo / Normal / Hyper bands with severity 1–5.
2. **Ketone Thresholds** — Ketone level bands with action descriptions.
3. **Blood Pressure Ranges** — Systolic/diastolic bands with OR/AND logic, category, and severity.
4. **Pulse Rate Ranges** — BPM bands with category and severity.

All four systems support a **two-tier scope model**: global defaults that apply to all Care Recipients, and per-CR overrides that replace the global defaults for a specific individual. Classification ranges drive automated alert thresholds, protocol triggers, and push notifications throughout the application.

---

## Scope Model

```
For a given CR read:
  1. Query ranges WHERE care_recipient_id = CR_id
  2. If any per-CR ranges exist → use ONLY those
  3. Else → use global defaults (WHERE care_recipient_id IS NULL)
```

This means a per-CR override replaces the entire global set, not just individual rows. If a per-CR range set is defined, the global defaults are ignored entirely.

---

## 1. BGL Classification Ranges

### Backend — `BglClassificationController`

**File:** `server/src/Vitara.Api/Controllers/BglClassificationController.cs`

Base route: `/api/bgl-classification`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/ranges` | `AllRoles` | Returns ranges (filtered by optional `careRecipientId` query param) |
| GET | `/ranges/{id}` | `AllRoles` | Returns a single range |
| POST | `/ranges` | `AdminOrHigher` | Creates a new range |
| PUT | `/ranges/{id}` | `AdminOrHigher` | Updates a range |
| DELETE | `/ranges/{id}` | `AdminOrHigher` | Deletes a range |
| GET | `/ketone-thresholds` | `AllRoles` | Returns ketone thresholds |
| POST | `/ketone-thresholds` | `AdminOrHigher` | Creates a ketone threshold |
| PUT | `/ketone-thresholds/{id}` | `AdminOrHigher` | Updates a ketone threshold |
| DELETE | `/ketone-thresholds/{id}` | `AdminOrHigher` | Deletes a ketone threshold |

### Model — `BglClassificationRange`

**File:** `server/src/Vitara.Api/Models/BglClassificationRange.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Label` | `string` | e.g., `"Severe Hypo"` |
| `MinValue` | `decimal?` | Null = no lower bound |
| `MaxValue` | `decimal?` | Null = no upper bound |
| `BglStateId` | `int` | 1=Normal, 2=Hypo, 3=Hyper |
| `SeverityId` | `int` | 1 (mildest) – 5 (most severe) |
| `CareRecipientId` | `int?` | Null = global default; set = per-CR override |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

### Service — `BglClassificationService`

**File:** `server/src/Vitara.Api/Services/BglClassificationService.cs`

#### `ClassifyReadingAsync(decimal bglValue, int? careRecipientId)`

1. Queries ranges for `careRecipientId` (if provided).
2. If no per-CR ranges found, falls back to global defaults.
3. Iterates all ranges; a range matches if `bglValue >= MinValue (or MinValue is null) AND bglValue < MaxValue (or MaxValue is null)`.
4. Returns the matching `BglClassificationRange` (or null if no match).

#### `GetRangesAsync(int? careRecipientId)`

Returns the effective range set for the given CR (per-CR if available, else global).

### DTO — `CreateBglRangeDto`

| Field | Type | Required | Notes |
|---|---|---|---|
| `label` | `string` | Yes | |
| `minValue` | `decimal?` | No | |
| `maxValue` | `decimal?` | No | |
| `bglStateId` | `int` | Yes | 1, 2, or 3 |
| `severityId` | `int` | Yes | 1–5 |
| `careRecipientId` | `int?` | No | |

---

## 2. Ketone Classification Thresholds

Ketone thresholds are a sub-resource under `BglClassificationController`.

### Model — `KetoneThreshold`

**File:** `server/src/Vitara.Api/Models/KetoneThreshold.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Label` | `string` | e.g., `"High Ketones"` |
| `MaxValue` | `decimal` | Upper bound for this band |
| `ActionDescription` | `string` | Clinical action guidance text |
| `CareRecipientId` | `int?` | Scope: null = global |
| `SortOrder` | `int` | Display ordering |

Ketone thresholds are evaluated as ascending bands: the first threshold where `ketoneValue < MaxValue` is the matching band.

### Usage

The `IncidentComponent` uses ketone thresholds when `type = 'DKA'` or when the form's ketone field is populated. The matched threshold's `ActionDescription` is displayed in the protocol step panel.

---

## 3. Blood Pressure Classification Ranges

### Backend — `BpClassificationRangeController`

**File:** `server/src/Vitara.Api/Controllers/BpClassificationRangeController.cs`

Base route: `/api/bp-classification`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/` | `AllRoles` | Returns ranges (filtered by optional `careRecipientId`) |
| GET | `/{id}` | `AllRoles` | Single range |
| POST | `/` | `AdminOrHigher` | Create range |
| PUT | `/{id}` | `AdminOrHigher` | Update range |
| DELETE | `/{id}` | `AdminOrHigher` | Delete range (seed rows protected) |

### Model — `BpClassificationRange`

**File:** `server/src/Vitara.Api/Models/BpClassificationRange.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Label` | `string` | e.g., `"Stage 2 Hypertension"` |
| `MinSystolic` | `int?` | Lower systolic bound (inclusive) |
| `MaxSystolic` | `int?` | Upper systolic bound (exclusive) |
| `MinDiastolic` | `int?` | Lower diastolic bound (inclusive) |
| `MaxDiastolic` | `int?` | Upper diastolic bound (exclusive) |
| `IsOrLogic` | `bool` | If true: match if systolic OR diastolic matches; if false: both must match |
| `Category` | `string` | `Hypotension`, `Normal`, `Elevated`, `Stage1`, `Stage2`, `Crisis` |
| `SeverityId` | `int` | 1–5 |
| `SortOrder` | `int` | Determines priority when multiple ranges match |
| `CareRecipientId` | `int?` | Null = global seed |

### OR vs AND Logic

Most AHA (American Heart Association) BP categories use OR logic — a reading qualifies if either systolic OR diastolic falls in the range. For example:

> Stage 1 Hypertension: SBP 130–139 **OR** DBP 80–89.

A single BP session reading is matched to the range with the **highest `SortOrder`** among all matching ranges. This ensures the most severe matching category wins.

#### Matching Algorithm (`BpClassificationService.ClassifyAsync`)

```
For each range (ordered by SortOrder descending):
  systolicMatch = (minSystolic == null OR sbp >= minSystolic) AND (maxSystolic == null OR sbp < maxSystolic)
  diastolicMatch = (minDiastolic == null OR dbp >= minDiastolic) AND (maxDiastolic == null OR dbp < maxDiastolic)

  if IsOrLogic:
    match = systolicMatch OR diastolicMatch
  else:
    match = systolicMatch AND diastolicMatch

  if match → return this range (first match wins due to descending sort)
```

### Global Seed Data (6 Ranges)

The seeder creates 6 global default ranges at startup if none exist:

| Label | SBP Range | DBP Range | OR? | Category | Severity |
|---|---|---|---|---|---|
| Normal | < 120 | < 80 | AND | Normal | 1 |
| Elevated | 120–129 | < 80 | AND | Elevated | 2 |
| Stage 1 Hypertension | 130–139 | 80–89 | OR | Stage1 | 3 |
| Stage 2 Hypertension | ≥ 140 | ≥ 90 | OR | Stage2 | 4 |
| Hypertensive Crisis | > 180 | > 120 | OR | Crisis | 5 |
| Hypotension | < 90 | < 60 | OR | Hypotension | 2 |

The `GLOBAL_RANGE_COUNT = 6` constant is used to determine which rows are seed rows; seed rows cannot be deleted via the API.

---

## 4. Pulse Rate Classification Ranges

### Backend — `PulseRateClassificationRangeController`

**File:** `server/src/Vitara.Api/Controllers/PulseRateClassificationRangeController.cs`

Base route: `/api/pulse-rate-classification`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/` | `AllRoles` | Returns ranges |
| POST | `/` | `AdminOrHigher` | Create range |
| PUT | `/{id}` | `AdminOrHigher` | Update range |
| DELETE | `/{id}` | `AdminOrHigher` | Delete (seed rows protected) |

### Model — `PulseRateClassificationRange`

**File:** `server/src/Vitara.Api/Models/PulseRateClassificationRange.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Label` | `string` | e.g., `"Tachycardia"` |
| `MinBpm` | `int?` | Lower bound (inclusive) |
| `MaxBpm` | `int?` | Upper bound (exclusive) |
| `Category` | `string` | `VeryLow`, `Low`, `Normal`, `High` |
| `SeverityId` | `int` | 1–5 |
| `SortOrder` | `int` | Matching priority |
| `CareRecipientId` | `int?` | Null = global |

### Global Seed Data (4 Ranges)

| Label | BPM Range | Category | Severity |
|---|---|---|---|
| Bradycardia (Severe) | < 40 | VeryLow | 5 |
| Bradycardia | 40–59 | Low | 3 |
| Normal | 60–100 | Normal | 1 |
| Tachycardia | > 100 | High | 3 |

The `GLOBAL_RANGE_COUNT = 4` constant guards seed rows from deletion.

### `PulseRateClassificationService.ClassifyAsync`

Same simple range scan: finds the first range (sorted by SortOrder desc) where `bpm >= MinBpm AND bpm < MaxBpm`. Returns the matching `PulseRateClassificationRange`.

---

## Frontend Components

### Shared Scope Selector Pattern

All four management components use the same care-recipient autocomplete input:
- Typing in the field calls `GET /carerecipient` and filters results.
- Selecting a CR sets the scope to per-CR; showing only that CR's ranges (or global ranges if none).
- Clearing the selector resets to global defaults view.

### `BglClassificationManagementComponent`

- CRUD table with search.
- BGL State dropdown: Normal / Hypo / Hyper.
- Min/Max fields: optional (null = unbounded).
- Severity 1–5 selector.

### `KetoneClassificationManagementComponent`

- CRUD table.
- Fields: Label, MaxValue, ActionDescription, SortOrder.
- ActionDescription is a textarea (rich guidance text).

### `BpClassificationManagementComponent`

- CRUD table with OR/AND logic toggle.
- Category dropdown: Hypotension / Normal / Elevated / Stage1 / Stage2 / Crisis.
- Delete disabled on the 6 global seed rows (detected by SortOrder or a `IsSeeded` flag).

### `PulseRateClassificationManagementComponent`

- CRUD table.
- Category dropdown: VeryLow / Low / Normal / High.
- Delete disabled on the 4 global seed rows.

---

## Classification in Clinical Features

| Feature | Uses |
|---|---|
| BGL Assessment | `BglClassificationService` — classifies each step reading |
| Diabetes Incident | `BglClassificationService` — classifies BGL on create |
| BP Session | `BpClassificationService` + `PulseRateClassificationService` |
| BP Incident | `BpClassificationService` |
| Supply projection | Not classification-based |
| Dashboard cards | `hasHbpConditions()` checks conditions, not BP ranges |

The classification result (label, severityId, category) is stored on the read record at creation time. Changing classification ranges does **not** retroactively update historical records.
