# Supplies

## Overview

The Supplies feature tracks medical supply inventories for care recipients. Each supply record stores an item (from a managed catalog or a custom name), usage rate, last order date, and total quantity ordered. The server computes a projection — quantity remaining, days until run-out, and suggested reorder date — every time the list is fetched. Staff and carers can manage supply records; only Administrators can manage the supply item catalog. A weekly supply report is dispatched automatically (via a GitHub Actions cron trigger) to notify carers when a care recipient's supplies are running low.

---

## Role Access

| Action | SuperAdmin | Administrator | Carer | SupportWorker | HealthCareProvider | CareRecipient |
|---|---|---|---|---|---|---|
| View own/linked CR supplies | ✓ | ✓ | ✓ | ✓ | — | Read own |
| Create supply records | ✓ | ✓ | ✓ | ✓ | — | — |
| Edit supply records | ✓ | ✓ | ✓ | ✓ | — | — |
| Delete supply records | ✓ | ✓ | ✓ | ✓ | — | — |
| Manage supply catalog | ✓ | ✓ | — | — | — | — |

---

## Backend

### Controller: `SupplyController` — `/api/supplies`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/supplies?careRecipientId=` | Authorized | Supply projections for a care recipient |
| POST | `/api/supplies` | SupportOrHigher | Create a supply record |
| PUT | `/api/supplies/{id}` | SupportOrHigher | Update a supply record |
| DELETE | `/api/supplies/{id}` | SupportOrHigher | Soft-delete (IsActive = false) |

### Controller: `SupplyItemController` — `/api/supply-items`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/supply-items` | Authorized | Active catalog items |
| GET | `/api/supply-items/all` | AdminOrHigher | All items including inactive |
| POST | `/api/supply-items` | AdminOrHigher | Create catalog item |
| PUT | `/api/supply-items/{id}` | AdminOrHigher | Update catalog item |
| DELETE | `/api/supply-items/{id}` | AdminOrHigher | Toggle active/inactive |

### Controller: `SupplyReportController` — `/api/supply-report`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/supply-report/send` | Bearer token (not JWT) | Triggered by cron; sends weekly supply emails + push |
| GET | `/api/supply-report/preview?careRecipientId=` | AdminOrHigher | Preview projection without sending |

### Business Logic — POST `/api/supplies`

1. Validates that `lastOrderDate` is not in the future.
2. If `supplyItemId` provided, checks the item exists and is active.
3. One of `supplyItemId` or `customItemName` must be present.
4. Creates the `Supply` row; `IsActive = true`.
5. Computes and returns the projection via `SupplyProjectionService`.

### Service: `SupplyProjectionService`

**File:** `server/src/Vitara.Api/Services/SupplyProjectionService.cs`

**Projection algorithm:**
```
daysSinceLastOrder = (today - lastOrderDate).TotalDays
usagePerDay        = usageQuantity / daysPerUnit   // daysPerUnit: Day=1, Week=7, Month=30, Year=365
qtyLeft            = max(0, totalOrdered − usagePerDay × daysSinceLastOrder)
daysUntilRunOut    = qtyLeft / usagePerDay          // INT_MAX if usagePerDay == 0
runOutDate         = today + daysUntilRunOut
effectiveLeadDays  = leadTimeDays ?? SupplyReorderLeadDays (from AppConfig)
reorderByDate      = runOutDate − effectiveLeadDays
isReorderDue       = today >= reorderByDate
isStale            = lastOrderDate < today − 365 days
```

Returns `SupplyProjectionDto`:
```
supplyId          int
itemName          string
careRecipientName string
usageDisplay      string       e.g. "5.00 per week"
usageQuantity     decimal
usageUnit         string
lastOrderDate     DateTime
totalOrdered      decimal
qtyLeft           decimal
daysUntilRunOut   int          INT_MAX means "no usage rate"
runOutDate        DateTime?
reorderByDate     DateTime?
isReorderDue      bool
isStale           bool
leadTimeDays      int?
notes             string?
isActive          bool
```

### Service: `SupplyReportService`

**File:** `server/src/Vitara.Api/Services/SupplyReportService.cs`

- **Idempotency:** Reads `SupplyReportLastSentAt` from `AppConfigService`. If the last send was within half the configured `SupplyReportIntervalDays`, returns without doing anything.
- **Target selection:** For each care recipient that has at least one active supply with `DaysUntilRunOut < SupplyAlertThresholdDays`, gathers the supplies.
- **Email:** Sends an HTML email to each linked carer + the `SupplyReportExtraEmails` list. Email contains a formatted table of items nearing run-out.
- **Push:** Sends `SupplyRunningLow` push (type 11) to each linked carer.
- Updates `SupplyReportLastSentAt` AppConfig key after sending.

### Key DTOs

**`CreateSupplyDto`**
```
careRecipientId   int
supplyItemId      int?       Omit if using customItemName
customItemName    string?    Free-text item name (mutually exclusive with supplyItemId)
usageQuantity     decimal    Min > 0
usageUnit         string     "PerDay" | "PerWeek" | "PerMonth" | "PerYear"
lastOrderDate     DateTime   Must not be in the future
totalOrdered      decimal    Min > 0
leadTimeDays      int?       Overrides global AppConfig default
notes             string?
```

**`UpdateSupplyDto`** — same fields as Create (all optional).

### Model: `Supply`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `CareRecipientId` | int | FK → User |
| `SupplyItemId` | int? | FK → SupplyItem (null for custom) |
| `CustomItemName` | string? | Used when supplyItemId is null |
| `UsageQuantity` | decimal (10,4) | |
| `UsageUnit` | UsageUnit enum | PerDay/PerWeek/PerMonth/PerYear |
| `LastOrderDate` | DateTime | |
| `TotalOrdered` | decimal | |
| `LeadTimeDays` | int? | Per-record override |
| `Notes` | string? | |
| `IsActive` | bool | Soft-delete flag |
| `CreatedByUserId` | int | FK → User |
| `CreatedAt` | DateTime | |
| `LastUpdatedById` | int? | |
| `LastUpdatedAt` | DateTime? | |

### Model: `SupplyItem` (catalog)

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `Name` | string (100) | Unique |
| `Description` | string? | |
| `IsActive` | bool | |
| `CreatedAt` | DateTime | |
| `Conditions` | many-to-many | Linked health conditions |

---

## Frontend

### Routes

| Path | Component | Guard |
|---|---|---|
| `/supplies` | `SupplyListComponent` | `AuthGuard` |
| `/supplies/admin` | `SupplyItemManagementComponent` | `AuthGuard` + AdminOrHigher |

### `SupplyListComponent` — `/supplies`

**File:** `client/src/app/features/supplies/components/supply-list.component.ts`

#### Care Recipient Selector
- **CareRecipient:** Auto-loads own supplies; no selector shown.
- **Admin/SuperAdmin:** Loads all active CRs via `CareRecipientService.getAllActiveCareRecipients()`; shows a dropdown.
- **Carer/SupportWorker:** Loads linked CRs; shows a dropdown.

#### Supply Table
Columns: Item Name | Usage | Total Ordered | Qty Left | Days Until Run Out | Run Out Date | Reorder By | Notes | Actions

**Colour coding of "Days Until Run Out":**
- Green (≥ 30 days)
- Orange (15–29 days)
- Red (< 15 days)
- N/A badge when `daysUntilRunOut === INT_MAX` (no usage rate set)

**Reorder Due:** A "Reorder Due" chip appears when `isReorderDue = true`.

**Stale indicator:** An asterisk footnote when `isStale = true` (last order was > 1 year ago; projection may be unreliable).

#### Add/Edit Supply
"Add Supply" button and the edit icon both open `SupplyFormComponent` as a `MatDialog` (full-screen on mobile).

### `SupplyFormComponent` (Dialog)

**File:** `client/src/app/features/supplies/components/supply-form.component.ts`

**On init:** Loads catalog items from `SupplyService.getSupplyItems()` (`GET /supply-items`).

**Fields:**
- **Item selector:** Dropdown of catalog items; OR a "Use Custom Name" toggle to reveal a free-text input.
- On edit: tries to match the existing `itemName` to a catalog item; falls back to custom mode if not found.
- `usageQuantity` — decimal, required, > 0.
- `usageUnit` — dropdown: Per Day / Per Week / Per Month / Per Year.
- `lastOrderDate` — date picker, max today.
- `totalOrdered` — decimal, required, > 0.
- `leadTimeDays` — optional integer.
- `notes` — optional textarea.

**Create:** `SupplyService.createSupply(dto)` → `POST /supplies`.
**Edit:** `SupplyService.updateSupply(id, dto)` → `PUT /supplies/{id}`.

On success, emits `true` to the dialog ref and the parent refreshes projections.

### `SupplyItemManagementComponent` — `/supplies/admin`

**File:** `client/src/app/features/supplies/components/supply-item-management.component.ts`

- Lists all catalog items via `SupplyService.getAllSupplyItems()` (`GET /supply-items/all`).
- Toggle "Show inactive items" to include/exclude soft-deleted items.
- Client-side search on name and description.
- Shows active/inactive badge and linked conditions.

**Create/Edit inline form:**
- `name` (required, max 100).
- `description` (optional, max 500).
- `conditions` — multi-select of available health conditions from `SupplyService.getConditions()` (`GET /conditions`).

**Toggle active:** `PATCH /supply-items/{id}/toggle-active` — activates or deactivates without deleting.

**Create:** `POST /supply-items`. **Edit:** `PUT /supply-items/{id}`.

### Feature Service: `SupplyService`

**File:** `client/src/app/features/supplies/services/supply.service.ts`

| Method | HTTP | Endpoint |
|---|---|---|
| `getSupplies(careRecipientId)` | GET | `/supplies?careRecipientId=N` |
| `createSupply(dto)` | POST | `/supplies` |
| `updateSupply(id, dto)` | PUT | `/supplies/{id}` |
| `deleteSupply(id)` | DELETE | `/supplies/{id}` |
| `getSupplyItems()` | GET | `/supply-items` |
| `getAllSupplyItems()` | GET | `/supply-items/all` |
| `createSupplyItem(dto)` | POST | `/supply-items` |
| `updateSupplyItem(id, dto)` | PUT | `/supply-items/{id}` |
| `toggleSupplyItemActive(id)` | PATCH | `/supply-items/{id}/toggle-active` |
| `getConditions()` | GET | `/conditions` |

---

## AppConfig Keys

All keys are managed via **Management → App Configuration → Supplies** (AdminOrHigher).

| Key | Default | Type | Secret | Read-Only | Description |
|---|---|---|---|---|---|
| `SupplyReorderLeadDays` | `7` | int | — | — | Global fallback lead-time days used in the reorder-by date calculation. Can be overridden per supply record with `leadTimeDays`. |
| `SupplyAlertThresholdDays` | `60` | int | — | — | Items with more than this many days of supply remaining are excluded from the weekly report. |
| `SupplyReportIntervalDays` | `7` | int | — | — | Minimum number of days between automated report sends. Used with `SupplyReportLastSentAt` to prevent duplicate sends on retries. |
| `SupplyReportExtraEmails` | _(empty)_ | string | — | — | Comma-separated email addresses to CC on every weekly supply report, in addition to each care recipient's linked carers. |
| `SupplyReportToken` | _(empty)_ | string | ✓ | — | Bearer token the GitHub Actions workflow must present to authenticate the `POST /api/supply-report/send` request. Must match the `SUPPLY_REPORT_TOKEN` GitHub repository secret. |
| `SupplyReportLastSentAt` | _(empty)_ | string | — | ✓ | ISO-8601 timestamp written by the server after each successful report send. Used for idempotency — do not edit manually. |

---

## Weekly Supply Report Trigger

The supply report is triggered externally via a GitHub Actions cron workflow (`.github/workflows/supply-report.yml`).

### How it works

1. GitHub Actions runs on a schedule (Saturday 08:00 AEST / 09:00 AEDT).
2. The workflow calls `POST /api/supply-report/send` with the `SUPPLY_REPORT_TOKEN` secret as a Bearer token.
3. The server validates the token against the `SupplyReportToken` AppConfig value. No JWT is used.
4. `SupplyReportService.SendWeeklyReportAsync()` runs the idempotency check, selects affected care recipients, sends emails and push notifications, then updates `SupplyReportLastSentAt`.

```
POST /api/supply-report/send
Authorization: Bearer <SupplyReportToken>
```

### Cron schedule

```yaml
# Friday 22:00 UTC = Saturday 08:00 AEST / 09:00 AEDT (Melbourne)
- cron: '0 22 * * 5'
```

The workflow can also be triggered manually from the **GitHub Actions** tab using `workflow_dispatch`.

### Setup checklist

| Step | Where |
|---|---|
| Generate a secure random token | e.g. `openssl rand -base64 32` |
| Set `SUPPLY_REPORT_TOKEN` repository secret | GitHub → Settings → Secrets and variables → Actions |
| Set `VITARA_API_URL` repository variable | GitHub → Settings → Secrets and variables → Actions → Variables |
| Set **Supply Report API Token** to the same value | Management → App Configuration → Supplies |

---

## Push Notifications Sent

| Trigger | Type | Recipients |
|---|---|---|
| Weekly report: supply running low | `SupplyRunningLow` (11) | All linked carers of affected CRs |
