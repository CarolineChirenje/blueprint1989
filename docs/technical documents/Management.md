# Management (Admin Hub)

## Overview

The Management section is an admin-only area (Admin and SuperAdmin roles) providing centralised control over the application's operational data. It consists of four sub-pages covering user management, expense cycle management, feature/bug report handling, and application configuration. All management routes are behind a `RoleGuard` restricting access to `Admin` and `SuperAdmin`.

---

## Role Access

All pages require `Admin` or `SuperAdmin`. Some actions (e.g., managing roles) require `SuperAdmin` only.

---

## Frontend Routes (ManagementModule)

| Path | Component |
|---|---|
| `/management/users` | `UserManagementComponent` |
| `/management/cycles` | `CycleManagementComponent` |
| `/management/feature-bug-reports` | `FeatureBugReportsComponent` |
| `/management/app-config` | `AppConfigManagementComponent` |

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
- Shows all 5 notification types as toggle switches.
- Save calls `PUT /admin/notification-preferences/{userId}`.

---

## 2. Cycle Management — `/management/cycles`

**Frontend File:** `client/src/app/features/management/components/cycle-management.component.ts`

**Backend:** `GET /cycle`, `POST /cycle`, `PUT /cycle/{id}`, `DELETE /cycle/{id}`

#### Cycle List
Shows all cycles: Name | Start Date | End Date | Status | Actions.

#### Create/Edit Form (Inline)
Fields: `name` (required), `startDate`, `endDate`, `members` (user selector).

#### Delete
Confirmation dialog. Active cycles cannot be deleted.

*For full documentation of the cycles feature, see [Cycles.md](Cycles.md).*

---

## 3. Feature/Bug Report Management — `/management/feature-bug-reports`

**Frontend File:** `client/src/app/features/management/components/feature-bug-reports.component.ts`

**Backend:** `GET /feature-bug-reports`, `PUT /feature-bug-reports/{id}/status`, `DELETE /feature-bug-reports/{id}`

*See [Feature-Bug-Reports.md](Feature-Bug-Reports.md) for full documentation.*

Summary:
- Lists all user-submitted reports with filter controls (status, type, priority).
- View dialog: full report detail; admin can update status.
- Close dialog: admin enters resolution notes.
- Delete with confirmation.

---

## 4. App Configuration — `/management/app-config`

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

## Shared Backend Endpoints Used

| Endpoint | Used By |
|---|---|
| `GET /users` | UserManagementComponent |
| `PUT /users/{id}` | UserManagementComponent |
| `GET/PUT /admin/notification-preferences/{userId}` | UserManagementComponent |
| `GET/POST/PUT/DELETE /expense-cycle` | CycleManagementComponent |
| `GET/PUT/DELETE /feature-bug-reports` | FeatureBugReportsComponent |
| `GET/PUT /app-config` | AppConfigManagementComponent |
