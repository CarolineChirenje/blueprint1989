# Management (Admin Hub)

## Overview

The Management section is an admin-only area (Administrator and SuperAdmin roles) providing centralised control over the application's operational data. It consists of eleven sub-pages covering user management, care cycle management, medical classification ranges, care recipient administration, delink request processing, feature/bug report handling, application configuration, and health condition management. All management routes are behind a `RoleGuard` restricting access to `Administrator` and `SuperAdmin`.

---

## Role Access

All pages require `Administrator` or `SuperAdmin`. Some actions (e.g., managing roles) require `SuperAdmin` only.

---

## Frontend Routes (ManagementModule)

| Path | Component |
|---|---|
| `/management/users` | `UserManagementComponent` |
| `/management/cycles` | `CycleManagementComponent` |
| `/management/bgl-classification` | `BglClassificationManagementComponent` |
| `/management/ketone-classification` | `KetoneClassificationManagementComponent` |
| `/management/bp-classification` | `BpClassificationManagementComponent` |
| `/management/pulse-rate-classification` | `PulseRateClassificationManagementComponent` |
| `/management/care-recipients` | `CareRecipientManagementComponent` |
| `/management/delink-requests` | `DelinkRequestManagementComponent` |
| `/management/feature-bug-reports` | `FeatureBugReportsComponent` |
| `/management/app-config` | `AppConfigManagementComponent` |
| `/management/conditions` | `ConditionManagementComponent` |

---

## 1. User Management — `/management/users`

**Frontend File:** `client/src/app/features/management/components/user-management.component.ts`

**Backend:** `GET /users`, `PUT /users/{id}`

#### On Init
Calls `GET /users` to load all users across all roles.

#### User Table
Columns: Email | First Name | Last Name | Role | Actions

Client-side search across email, first name, last name.

#### Edit User (Inline)
Clicking the edit icon on a row opens an inline editing form with:
- `email` (text field).
- `firstName`, `lastName`.
- `role` (dropdown of all 6 roles).

Save calls `PUT /users/{id}`. On success, the row updates in place.

#### Notification Preferences (per user)
Bell icon button per row opens a full preferences modal:
- Loads `GET /admin/notification-preferences/{userId}`.
- Shows all 12 notification types as toggle switches (Admin-controlled types included).
- Save calls `PUT /admin/notification-preferences/{userId}`.

---

## 2. Cycle Management — `/management/cycles`

**Frontend File:** `client/src/app/features/management/components/cycle-management.component.ts`

**Backend:** `GET /cycle`, `POST /cycle`, `PUT /cycle/{id}`, `DELETE /cycle/{id}`

#### Cycle List
Shows all cycles: Name | Year | Default badge | Actions.

#### Create/Edit Form (Inline)
Fields: `name` (required), `year` (dropdown, current year ±10), `isDefault` toggle.

Saving a cycle as default clears the previous default.

#### Delete
Confirmation dialog. If the deleted cycle was the JWT's `termId`, the user's next login will pick up the new default.

*For full documentation of the cycles feature, see [Cycles-Terms.md](Cycles-Terms.md).*

---

## 3. BGL Classification Management — `/management/bgl-classification`

**Frontend File:** `client/src/app/features/management/components/bgl-classification-management.component.ts`

**Backend:** `GET/POST/PUT/DELETE /bgl-classification/ranges`

#### Scope Selector
Autocomplete care-recipient input. Leave empty = global defaults. Selecting a CR shows per-CR override ranges.

#### Range Table
Columns: Label | Min Value | Max Value | BGL State | Severity | Actions

Client-side search by label.

#### Create/Edit Form
Fields: `label`, `minValue` (decimal, nullable), `maxValue` (decimal, nullable), `bglStateId` (1=Normal, 2=Hypo, 3=Hyper), `severityId` (1–5), `careRecipientId?`.

*For full classification documentation, see [Classification-Management.md](Classification-Management.md).*

---

## 4. Ketone Classification Management — `/management/ketone-classification`

**Frontend File:** `client/src/app/features/management/components/ketone-classification-management.component.ts`

**Backend:** `GET/POST/PUT/DELETE /bgl-classification/ketone-thresholds`

Manages ketone threshold bands (label, max value, action description) with the same global/per-CR scope selector pattern.

*See [17-classification-management.md](17-classification-management.md).*

---

## 5. BP Classification Management — `/management/bp-classification`

**Frontend File:** `client/src/app/features/management/components/bp-classification-management.component.ts`

**Backend:** `GET/POST/PUT/DELETE /bp-classification`

#### Scope Selector
Same care-recipient autocomplete. Global defaults = 6 permanent seed ranges (`GLOBAL_RANGE_COUNT = 6`); seed rows cannot be deleted.

#### Range Table
Columns: Label | Min Systolic | Max Systolic | Min Diastolic | Max Diastolic | OR Logic | Category | Severity | Sort Order | Actions

#### Create/Edit Form
Fields: `label`, `minSystolic?`, `maxSystolic?`, `minDiastolic?`, `maxDiastolic?`, `isOrLogic` toggle, `category` (Hypotension–HypertensiveCrisis), `severity` (1–5), `sortOrder`.

*See [Classification-Management.md](Classification-Management.md).*

---

## 6. Pulse Rate Classification Management — `/management/pulse-rate-classification`

**Frontend File:** `client/src/app/features/management/components/pulse-rate-classification-management.component.ts`

**Backend:** `GET/POST/PUT/DELETE /pulse-rate-classification`

Same scope-selector pattern. 4 global seed rows cannot be deleted.

#### Create/Edit Form
Fields: `label`, `minBpm?`, `maxBpm?`, `category` (VeryLow/Low/Normal/High), `severity` (1–5), `sortOrder`.

*See [Classification-Management.md](Classification-Management.md).*

---

## 7. Care Recipient Management — `/management/care-recipients`

*See [Care-Recipients.md](Care-Recipients.md) for full documentation.*

Summary: Lists all CRs (including inactive), expandable rows with linked carers, admin delink capability.

---

## 8. Delink Request Management — `/management/delink-requests`

*See [Care-Recipients.md](Care-Recipients.md) for full documentation.*

Summary: Lists pending delink requests submitted by CareRecipients. Admin approves or rejects each.

---

## 9. Feature/Bug Report Management — `/management/feature-bug-reports`

**Frontend File:** `client/src/app/features/management/components/feature-bug-reports.component.ts`

**Backend:** `GET /feature-bug-reports`, `PUT /feature-bug-reports/{id}/status`, `DELETE /feature-bug-reports/{id}`

*See [Feature-Bug-Reports.md](Feature-Bug-Reports.md) for full documentation.*

Summary:
- Lists all user-submitted reports with filter controls (status, type, priority).
- View dialog: full report detail; admin can update status.
- Close dialog: admin enters resolution notes.
- Delete with confirmation.

---

## 10. App Configuration — `/management/app-config`

**Frontend File:** `client/src/app/features/management/components/app-config-management.component.ts`

**Backend:** `GET /app-config`, `PUT /app-config/{key}`, `PUT /app-config/bulk`

*See [App-Configuration.md](App-Configuration.md) for full documentation.*

Summary:
- Full key-value editor grouped by category.
- Inline editing with per-key dirty tracking.
- "Save All" sends only changed keys in a bulk call.
- Secret values masked by default with reveal toggle.
- "Requires restart" banner shown when a saved key has `RequiresRestart = true`.

---

## 11. Condition Management — `/management/conditions`

**Frontend File:** `client/src/app/features/management/components/condition-management.component.ts`

**Backend:** `GET /conditions`, `PATCH /conditions/{id}/toggle`

#### On Init
Calls `GET /conditions` to load all health condition records.

#### Condition List
Columns: Condition Name | Active Status | Actions

#### Toggle Active
Clicking the toggle switch calls `PATCH /conditions/{id}/toggle`:
- If `IsActive = true` → marks inactive (condition no longer appears in signup or profile dropdowns).
- If `IsActive = false` → marks active again.

No create or delete is available through the UI — conditions (T1 Diabetes, T2 Diabetes, High Blood Pressure) are seeded during database initialisation and are not intended to be removed.

---

## Shared Backend Endpoints Used

| Endpoint | Used By |
|---|---|
| `GET /users` | UserManagementComponent |
| `PUT /users/{id}` | UserManagementComponent |
| `GET/PUT /admin/notification-preferences/{userId}` | UserManagementComponent |
| `GET/POST/PUT/DELETE /cycle` | CycleManagementComponent |
| `GET/POST/PUT/DELETE /bgl-classification/ranges` | BglClassificationManagementComponent |
| `GET/POST/PUT/DELETE /bgl-classification/ketone-thresholds` | KetoneClassificationManagementComponent |
| `GET/POST/PUT/DELETE /bp-classification` | BpClassificationManagementComponent |
| `GET/POST/PUT/DELETE /pulse-rate-classification` | PulseRateClassificationManagementComponent |
| `GET /carerecipient/admin/all` | CareRecipientManagementComponent |
| `GET `/carerecipient/admin/{id}/linked-carers` | CareRecipientManagementComponent |
| `DELETE /carerecipient/admin/{id}/linked-carer/{carerId}` | CareRecipientManagementComponent |
| `GET /delink-requests/pending` | DelinkRequestManagementComponent |
| `POST /delink-requests/{id}/approve` | DelinkRequestManagementComponent |
| `POST /delink-requests/{id}/reject` | DelinkRequestManagementComponent |
| `GET/PUT/DELETE /feature-bug-reports` | FeatureBugReportsComponent |
| `GET/PUT /app-config` | AppConfigManagementComponent |
| `GET/PATCH /conditions/{id}/toggle` | ConditionManagementComponent |
