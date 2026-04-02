# Cycles

## Overview

A **Cycle** (also called a Term) represents a named operational period — typically an annual care cycle (e.g., "2026 Care Cycle"). Every user session is bound to a specific term via the JWT `termId` claim. All clinical data (assessments, incidents, meal entries, BP sessions) is recorded under the active term, enabling historical comparison between care cycles.

Exactly one cycle can be the **default** at any time. On login, the server stamps the current default cycle's ID into the issued JWT token. Administrators can create, rename, manage, and switch the default cycle through the Management console.

---

## Role Access

| Role | Access |
|---|---|
| SuperAdmin | Full CRUD + set default |
| Administrator | Full CRUD + set default |
| Carer / SupportWorker / CareRecipient / HealthCareProvider | Read-only (term embedded in JWT) |

---

## Backend

### Controller — `CycleController`

**File:** `server/src/Vitara.Api/Controllers/CycleController.cs`

Base route: `/api/cycle`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/` | `AllRoles` | Returns all cycles |
| GET | `/{id}` | `AllRoles` | Returns a single cycle |
| POST | `/` | `AdminOrHigher` | Creates a new cycle |
| PUT | `/{id}` | `AdminOrHigher` | Updates name, year, or default flag |
| DELETE | `/{id}` | `AdminOrHigher` | Deletes a cycle |
| POST | `/{id}/set-default` | `AdminOrHigher` | Sets this cycle as the default |

---

### Model — `Cycle`

**File:** `server/src/Vitara.Api/Models/Cycle.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Name` | `string` | e.g., `"2026 Care Cycle"` |
| `Year` | `int` | Numeric year (used for display grouping) |
| `IsDefault` | `bool` | Only one row can be true at a time |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |
| `Assessments` | `ICollection<Assessment>` | Navigation |
| `Incidents` | `ICollection<Incident>` | Navigation |
| `BpSessions` | `ICollection<BloodPressureSession>` | Navigation |
| `MealEntries` | `ICollection<MealEntry>` | Navigation |

---

### Service — `CycleService`

**File:** `server/src/Vitara.Api/Services/CycleService.cs`

#### Key Methods

| Method | Description |
|---|---|
| `GetAllAsync()` | Returns all cycles ordered by year descending |
| `GetByIdAsync(id)` | Returns a single cycle or null |
| `GetDefaultAsync()` | Returns the cycle where `IsDefault = true` |
| `CreateAsync(dto)` | Creates a new cycle; if `IsDefault = true`, clears previous default first |
| `UpdateAsync(id, dto)` | Updates name/year; if `IsDefault` toggled on, clears previous default |
| `DeleteAsync(id)` | Deletes a cycle. If the cycle is the default, no implicit new default is set — admin must manually set a new default |
| `SetDefaultAsync(id)` | Sets `IsDefault = true` on target, `IsDefault = false` on all others |

#### Default-Clearing Logic

When creating or updating a cycle with `IsDefault = true`:

```csharp
var existing = await _db.Cycles.FirstOrDefaultAsync(c => c.IsDefault);
if (existing != null && existing.Id != id)
{
    existing.IsDefault = false;
}
```

This ensures uniqueness of the default without a database constraint (to allow bulk transitions).

---

### DTOs

#### `CreateCycleDto`

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | `string` | Yes | Max 100 chars |
| `year` | `int` | Yes | 4-digit year |
| `isDefault` | `bool` | No | Defaults to false |

#### `UpdateCycleDto`

Same fields as `CreateCycleDto`, all optional (PATCH semantics via PUT).

---

### JWT Term Binding

When a user logs in, `AuthService.LoginByEmailAsync` calls `CycleService.GetDefaultAsync()` and embeds the default cycle ID into the JWT as the `termId` claim:

```csharp
claims.Add(new Claim("termId", defaultCycle.Id.ToString()));
```

All subsequent API calls from that session carry the `termId` in the token. Clinical data creation endpoints read the `termId` from the JWT to tag new records:

```csharp
var termId = int.Parse(User.FindFirstValue("termId")!);
```

This means:
- Changing the default cycle does **not** affect existing sessions. Users must log out and back in for the new default to take effect.
- Historical data retains its original `termId`; filtering by cycle is done by comparing against stored `TermId` columns.
- There is no mechanism for a regular user to switch terms mid-session.

---

### Database Impact

All primary clinical tables have a `TermId` (or `CycleId`) foreign key column:

| Table | Column | Notes |
|---|---|---|
| `Assessments` | `TermId` | FK → Cycles |
| `Incidents` | `TermId` | FK → Cycles |
| `BloodPressureSessions` | `TermId` | FK → Cycles |
| `MealEntries` | `TermId` | FK → Cycles |
| `BpIncidents` | `TermId` | FK → Cycles |

EF Core cascade behaviour: deleting a Cycle with associated records will fail with a FK constraint violation unless all associated records are first deleted or reassigned.

---

## Frontend

### `CycleManagementComponent`

**File:** `client/src/app/features/management/components/cycle-management.component.ts`

Route: `/management/cycles`

#### Cycle List Table

Columns: Name | Year | Default Badge | Actions (edit, delete, set-default)

Rows ordered by Year descending.

#### Create / Edit Form (Inline)

Fields:
- `name` — required text input.
- `year` — `MatSelect` populated with current year and ±10 range.
- `isDefault` — `MatSlideToggle`.

Selecting `isDefault = true` on save will clear the badge on whichever row currently has it, and add it to the saved row.

#### Set Default Button

An explicit "Set as Default" icon button per non-default row. Calls `POST /cycle/{id}/set-default`. On success, updates the `isDefault` flag reactively in the local array.

#### Delete

Confirmation `MatDialog`. If the cycle has associated clinical data, the API will return a 400/409 error and the component shows a snackbar: "Cannot delete a cycle with existing records."

---

### CycleService (Angular)

**File:** `client/src/app/features/management/services/cycle.service.ts`

| Method | HTTP | Endpoint |
|---|---|---|
| `getAll()` | GET | `/api/cycle` |
| `getById(id)` | GET | `/api/cycle/{id}` |
| `create(dto)` | POST | `/api/cycle` |
| `update(id, dto)` | PUT | `/api/cycle/{id}` |
| `delete(id)` | DELETE | `/api/cycle/{id}` |
| `setDefault(id)` | POST | `/api/cycle/{id}/set-default` |

---

## End-to-End Term Flow

```
Admin creates "2027 Care Cycle" via /management/cycles
  → POST /api/cycle { name: "2027 Care Cycle", year: 2027, isDefault: true }
  → CycleService.CreateAsync clears previous default, creates new row
  → Response: { id: 7, isDefault: true }

Admin logs out and back in (or any user logs in after this point)
  → AuthService.LoginByEmailAsync → GetDefaultAsync() → id: 7
  → JWT issued with termId: 7

Carer records a BGL incident
  → POST /api/incident { ...fields... }
  → IncidentController reads User.FindFirstValue("termId") → 7
  → Incident.TermId = 7 persisted to DB

Admin runs a report for "2026 Care Cycle"
  → ExportService queries: WHERE term_id = 6 (2026 cycle id)
  → Returns only 2026 data regardless of current default
```
