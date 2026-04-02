# Feature & Bug Reports

## Overview

The Feature & Bug Reports system allows all authenticated users to submit requests for new features or report application defects. Each submission is categorized, prioritized, and tracked through a lifecycle of statuses. Admins manage the full list through the Management console; all users can view their own reports from the Profile section. A push notification is sent to all SuperAdmin users when a new report is submitted.

---

## Role Access

| Action | Roles |
|---|---|
| Submit a report | All roles |
| View own reports | All roles |
| View all reports | SuperAdmin, Administrator |
| Update status / close | SuperAdmin, Administrator |
| Delete a report | SuperAdmin, Administrator |

---

## Backend

### Controller — `FeatureBugReportController`

**File:** `server/src/Vitara.Api/Controllers/FeatureBugReportController.cs`

Base route: `/api/feature-bug-reports`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/` | `AdminOrHigher` | Lists all reports with optional filters |
| GET | `/my` | `AllRoles` | Returns reports submitted by the current user |
| GET | `/{id}` | `AllRoles` | Returns a single report (own only unless admin) |
| POST | `/` | `AllRoles` | Submits a new report |
| PUT | `/{id}/status` | `AdminOrHigher` | Updates report status |
| PUT | `/{id}/close` | `AdminOrHigher` | Closes the report with resolution notes |
| DELETE | `/{id}` | `AdminOrHigher` | Soft-deletes a report |

#### `GET /` — Query Parameters

| Parameter | Type | Description |
|---|---|---|
| `status` | `string?` | Filter by status (Open, InProgress, Closed, Rejected) |
| `type` | `string?` | Filter by type (Feature, Bug) |
| `priority` | `string?` | Filter by priority (Low, Medium, High, Critical) |
| `search` | `string?` | Text search in title/description |

---

### Model — `FeatureBugReport`

**File:** `server/src/Vitara.Api/Models/FeatureBugReport.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `UserId` | `int` | FK → User (submitter) |
| `Title` | `string` | Short summary (max 200 chars) |
| `Description` | `string` | Full description |
| `Type` | `string` | `"Feature"` or `"Bug"` |
| `Priority` | `string` | `"Low"`, `"Medium"`, `"High"`, `"Critical"` |
| `Status` | `string` | `"Open"`, `"InProgress"`, `"Closed"`, `"Rejected"` |
| `ResolutionNotes` | `string?` | Populated when closed/rejected |
| `IsDeleted` | `bool` | Soft-delete flag |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |
| `ClosedAt` | `DateTime?` | Populated when status becomes Closed |
| `User` | `User` | Navigation (submitter) |
| `Categories` | `ICollection<ReportCategory>` | Many-to-many join |

### Many-to-Many Categories

A report can belong to multiple categories (e.g., "Authentication", "Dashboard", "Notifications"). The join table `FeatureBugReportCategories` links `FeatureBugReportId` → `ReportCategoryId`.

#### `ReportCategory` Model

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Name` | `string` | Category label |
| `IsActive` | `bool` | Controls whether shown in submission form |

---

### Service — `FeatureBugReportService`

**File:** `server/src/Vitara.Api/Services/FeatureBugReportService.cs`

| Method | Description |
|---|---|
| `GetFilteredAsync(filters)` | Queries all non-deleted reports with optional filters applied |
| `GetForUserAsync(userId)` | Returns all non-deleted reports for a specific user |
| `GetByIdAsync(id, userId, isAdmin)` | Returns single report; non-admins can only retrieve their own |
| `CreateAsync(dto, userId)` | Creates report, sends push to all SuperAdmin users |
| `UpdateStatusAsync(id, status)` | Updates the `Status` field and `UpdatedAt` |
| `CloseAsync(id, notes)` | Sets `Status = "Closed"`, `ResolutionNotes = notes`, `ClosedAt = UtcNow` |
| `SoftDeleteAsync(id)` | Sets `IsDeleted = true` |

#### Push Notification on Submission

`CreateAsync` calls `PushNotificationSender` after saving:

```
Notification type: FeatureBugReportSubmitted
Title: "New [Type] Report Submitted"
Body:  "[UserName] submitted: [Title]"
Recipients: All users with role SuperAdmin (1)
```

---

### DTOs

#### `CreateReportDto`

| Field | Type | Required | Validation |
|---|---|---|---|
| `title` | `string` | Yes | Max 200 chars |
| `description` | `string` | Yes | Max 4000 chars |
| `type` | `string` | Yes | `"Feature"` or `"Bug"` |
| `priority` | `string` | Yes | `"Low"`, `"Medium"`, `"High"`, `"Critical"` |
| `categoryIds` | `int[]` | No | IDs of selected categories |

#### `UpdateStatusDto`

| Field | Type | Required |
|---|---|---|
| `status` | `string` | Yes |

#### `CloseReportDto`

| Field | Type | Required |
|---|---|---|
| `resolutionNotes` | `string` | Yes |

---

## Frontend

### `ReportFeatureBugComponent` (Submission Form)

**File:** `client/src/app/features/shared/report-feature-bug/report-feature-bug.component.ts`

Access: Floating action button or "Report Issue" menu item available on most pages. Opens as a `MatDialog`.

#### Form Controls

| Control | Type | Validation |
|---|---|---|
| `title` | `MatInput` | Required, max 200 |
| `description` | `MatInput` (textarea) | Required, max 4000 |
| `type` | `MatButtonToggle` | Required — Feature / Bug |
| `priority` | `MatButtonToggle` | Required — Low / Medium / High / Critical |
| `categories` | `MatChipListbox` (multi-select) | Optional — loaded from `GET /report-categories` |

On submit: calls `FeatureBugReportService.create(dto)`. On success, closes dialog and shows "Report submitted. Thank you!" snackbar.

---

### `MyReportsComponent`

**File:** `client/src/app/features/profile/components/my-reports.component.ts`

Route: `/profile/reports`

Displays the current user's submitted reports in a table:

| Column | Notes |
|---|---|
| Title | Truncated with tooltip |
| Type | "Feature" or "Bug" chip |
| Priority | Color-coded chip |
| Status | Color-coded chip |
| Date | Submitted date |
| Actions | View button → opens `ViewReportDialogComponent` |

No edit or delete available for regular users (reports are immutable after submission).

---

### `FeatureBugReportsComponent` (Admin View)

**File:** `client/src/app/features/management/components/feature-bug-reports.component.ts`

Route: `/management/feature-bug-reports`

#### Filters Row

- Status filter (`MatSelect`): All / Open / InProgress / Closed / Rejected.
- Type filter: All / Feature / Bug.
- Priority filter: All / Low / Medium / High / Critical.
- Text search (`MatInput`): filters by title/description client-side after data loads.

#### Report Table

Columns: Submitted By | Title | Type | Priority | Status | Submitted At | Actions

Actions per row:
- **View** — opens `ViewReportDialogComponent`.
- **Close / Reject** — opens `CloseReportDialogComponent`.
- **Delete** — confirmation prompt then calls `DELETE /{id}`.

#### `ViewReportDialogComponent`

Shows all report fields. Admin can change Status via a `MatSelect` inline; calls `PUT /{id}/status` on change.

Displays categories as chips, full description, submitter info, and any existing `ResolutionNotes`.

#### `CloseReportDialogComponent`

Simple form:
- Status selector: Closed / Rejected.
- `resolutionNotes` textarea (required).

On save: calls `PUT /{id}/close` with notes. Report status updates in the table.

---

### `FeatureBugReportService` (Angular)

**File:** `client/src/app/features/management/services/feature-bug-report.service.ts` (admin) or `client/src/app/core/services/feature-bug-report.service.ts` (shared)

| Method | HTTP | Endpoint |
|---|---|---|
| `getAll(filters?)` | GET | `/api/feature-bug-reports` |
| `getMy()` | GET | `/api/feature-bug-reports/my` |
| `getById(id)` | GET | `/api/feature-bug-reports/{id}` |
| `create(dto)` | POST | `/api/feature-bug-reports` |
| `updateStatus(id, status)` | PUT | `/api/feature-bug-reports/{id}/status` |
| `close(id, notes)` | PUT | `/api/feature-bug-reports/{id}/close` |
| `delete(id)` | DELETE | `/api/feature-bug-reports/{id}` |

---

## Report Lifecycle

```
User opens dialog → fills form → Submit
  → FeatureBugReportController.CreateAsync
  → Report saved with Status = "Open"
  → Push notification sent to all SuperAdmin users

Admin reviews report in /management/feature-bug-reports
  → Updates status to "InProgress" via ViewReportDialog
    → PUT /feature-bug-reports/{id}/status { status: "InProgress" }

Admin resolves issue
  → Opens CloseReportDialog
  → Sets status to "Closed", enters resolution notes
    → PUT /feature-bug-reports/{id}/close { resolutionNotes: "..." }
  → ClosedAt timestamp set on record

User views their report in /profile/reports
  → Sees status "Closed" with resolution notes
```
