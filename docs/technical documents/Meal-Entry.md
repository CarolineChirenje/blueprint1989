# Meal Entry

## Overview

The Meal Entry feature allows staff and carers to log food intake for care recipients, capturing the food item name, portion size, carbohydrates consumed, and carb level category. The primary purpose is bolus calculation support for diabetes management. Each entry can be later edited or deleted. A history view with client-side search provides an audit trail, and Excel export is available for reporting. CareRecipients themselves cannot edit or delete entries — only create them — preserving data accuracy.

---

## Role Access

| Role | Create | View | Edit | Delete | Export |
|---|---|---|---|---|---|
| SuperAdmin | ✓ | All | ✓ | ✓ | ✓ |
| Administrator | ✓ | All | ✓ | ✓ | ✓ |
| Carer | ✓ | Linked CRs | ✓ | ✓ | ✓ |
| SupportWorker | ✓ | Linked CRs | ✓ | ✓ | ✓ |
| CareRecipient | ✓ (own) | Own | — | — | ✓ |
| HealthCareProvider | — | Linked CRs | — | — | — |

---

## Backend

### Controller: `MealEntryController` — `/api/mealentry`

| Method | Route | Auth | Description |
|---|---|---|---|
| POST | `/api/mealentry/entries` | Authorized | Create a meal entry |
| GET | `/api/mealentry/entries` | Authorized | List entries (role-scoped) |
| GET | `/api/mealentry/entries/{id}` | Anonymous | Get a single entry |
| PUT | `/api/mealentry/entries/{id}` | Authorized (non-CR) | Update an entry |
| DELETE | `/api/mealentry/entries/{id}` | Authorized (non-CR) | Delete an entry |
| GET | `/api/mealentry/export/weekly` | Anonymous | Weekly entries export with optional date range |

### Business Logic

**POST `/api/mealentry/entries`**
- CareRecipient role: `CareRecipientId` is automatically set to `self` (from JWT `id` claim); the request's `careRecipientId` field is ignored.
- Other roles: `CareRecipientId` may be specified or null (entry attributed to the user themselves).

**PUT / DELETE `/api/mealentry/entries/{id}`**
- CareRecipient role: returns 403 Forbidden. Only Carer/SupportWorker/Admin roles may modify or delete entries.

**GET `/api/mealentry/entries`**
- CareRecipient: returns entries where `CareRecipientId == self` OR `UserId == self`.
- Other roles: accepts optional `?userId=N` query param to filter by user.

**GET `/api/mealentry/export/weekly`**
- Returns entries optionally filtered by `?startDate` and `?endDate` query params.

### Key DTOs

**`CreateMealEntryRequest`**
```
foodItem           string       Max 200 characters (required)
portionSize        decimal      Numeric portion amount (required)
portionUnit        PortionUnit  Enum (required) — see values below
carbsConsumed      decimal      Carbs in grams for the listed portion (required)
comments           string?      Optional free text
clientTimestamp    DateTime?    Client-supplied timestamp for offline entries
```

**`UpdateMealEntryRequest`**
```
foodItem      string
portionSize   decimal
portionUnit   PortionUnit
carbsConsumed decimal
comments      string?
```

**`MealEntryResponse`** (response)
```
id                  int
foodItem            string
portionSize         decimal
portionUnit         PortionUnit
carbsConsumed       decimal
carbLevel           CarbLevel      Auto-classified: Low / Medium / High
comments            string?
timestamp           DateTime
userId              int
userName            string
lastUpdatedByName   string?
lastUpdatedAt       DateTime?
```

### Model: `MealEntry`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `FoodItem` | string (200) | Free-text food name |
| `PortionSize` | decimal (7,2) | Numeric quantity (column: `portion_size`) |
| `PortionUnit` | PortionUnit enum | Unit of measurement (column: `portion_unit`) |
| `CarbsConsumed` | decimal (7,2) | Carbs in grams for the recorded portion |
| `CarbLevel` | CarbLevel enum | Auto-set by service: Low (≤15g) / Medium (16–70g) / High (>70g) |
| `Comments` | string? | |
| `Timestamp` | DateTime | Defaults to `NOW() AT TIME ZONE 'UTC'` |
| `UserId` | int | FK → User (creator) |
| `CareRecipientId` | int? | FK → User (care recipient the entry belongs to) |
| `LastUpdatedById` | int? | FK → User (last editor) |
| `LastUpdatedAt` | DateTime? | |

### Enums

**`PortionUnit`**
| Value | Label |
|---|---|
| Grams | g |
| Millilitres | ml |
| Cup | cup |
| Tablespoon | tbsp |
| Teaspoon | tsp |
| Medium | medium |
| Large | large |
| Small | small |
| Slice | slice |
| Piece | piece |
| Bar | bar |
| Tub | tub |
| Packet | packet |
| Plate | plate |

**`CarbLevel`** (auto-classified by `MealEntryService.ClassifyCarbLevel()`)
| Value | Threshold |
|---|---|
| Low | ≤ 15g |
| Medium | 16–70g |
| High | > 70g |

### `MealEntryService`

**File:** `server/src/Vitara.Api/Services/MealEntryService.cs`

**Dependencies:** `ApplicationDbContext`

Key methods:
- `AddMealEntryAsync(request, userId, careRecipientId?)` — creates entry; auto-classifies `CarbLevel`; uses `ClientTimestamp` if provided (offline support), otherwise `DateTime.UtcNow`.
- `UpdateMealEntryAsync(id, request, updatedByUserId)` — updates all fields; recalculates `CarbLevel`; sets `LastUpdatedById` and `LastUpdatedAt`.
- `DeleteMealEntryAsync(id)` — hard delete.
- `GetMealEntriesAsync(userId?, termId?, careRecipientId?)` — returns all entries ordered by timestamp descending; includes User and LastUpdatedByUser navigation.
- `GetMealEntriesForCareRecipientAsync(userId)` — returns entries where `CareRecipientId == userId` OR `UserId == userId`.
- `GetWeeklyMealEntriesAsync(startDate?, endDate?)` — date-range filter; defaults to last 7 days.
- `ClassifyCarbLevel(carbsConsumed)` — private helper: ≤15g → Low, 16–70g → Medium, >70g → High.

---

## Frontend

### Routes

| Path | Component | Notes |
|---|---|---|
| `/meal-entry/entry` | `MealEntryComponent` | Create or edit a single entry |
| `/meal-entry/history` | `MealEntryHistoryComponent` | View and manage all entries |

### `MealEntryComponent` — `/meal-entry/entry`

**File:** `client/src/app/features/meal-entry/components/meal-entry.component.ts`

**Modes:**
- **Create mode** (default): form is empty; submission calls `MealEntryService.createEntry(dto)`.
- **Edit mode**: activated when `?id=N` query param is present. On init, loads the entry via `MealEntryService.getEntry(id)`, pre-fills all fields, and submission calls `MealEntryService.updateEntry(id, dto)`.

**Form fields:**
- `foodItem` — required text input (max 200).
- `portionSize` — required decimal number input.
- `portionUnit` — required dropdown: Grams / Millilitres / Cup / Tablespoon / Teaspoon / Medium / Large / Small / Slice / Piece.
- `carbsConsumed` — required decimal input (the total carbs for the specified portion).
- `comments` — optional textarea.

**Offline fallback:** `MealEntryService.createEntry()` enqueues to `OfflineQueueService` (type `'meal-entry'`) on network failure.

**Navigation:** On successful create, navigates to `/meal-entry/history`. On successful edit, navigates back with a success snackbar.

### `MealEntryHistoryComponent` — `/meal-entry/history`

**File:** `client/src/app/features/meal-entry/components/meal-entry-history.component.ts`

**On init:** Calls `MealEntryService.getEntries()`.

**Table columns:** Food Item | Portion Size | Carbs (g) | Comments | Last Updated | Actions

**Carbs (g) column:** Displays the gram value followed by a colour-coded `CarbLevel` badge — green **Low**, blue **Medium**, orange **High** — matching the badge style used in the entry form's live feedback.

**Client-side search:** Filters across food item, portion unit, portion size, carbs, and comments fields.

**Edit action:** Navigates to `/meal-entry/entry?id=N`.

**Delete action:** Triggers `DialogService.confirmDelete()`, then calls `MealEntryService.deleteMealEntry(id)`. Hidden for CareRecipient role.

**Excel export:** Downloads `Bolus_History_Report_YYYY-MM-DD.xlsx` via `GET /api/export/mealentry-report`.

### Single-Item Carb Calculator (inline, per row)

A `calculate` icon button in each row's Actions cell opens an inline calculator panel beneath that row.

**Formula:** `(inputAmount / portionSize) × carbsConsumed` — proportional scaling.

**Example:** Logged entry is 100g fish = 20g carbs. User enters 150g → result: 30g.

**Unit behaviour:**
- For `Grams` and `Millilitres` (proportional units): label shows "Amount (g)" or "Amount (ml)".
- For non-proportional units (Cup, Slice, Piece, etc.): label shows the unit name; calculation still applies the same ratio.

Result is rounded to 1 decimal place. If the result has a fractional part, a rounded whole-gram figure is shown alongside with an asterisk note for bolus purposes.

**State:** `expandedEntry`, `calcInput`, `calcResult` on `MealEntryHistoryComponent`. Only one row can be expanded at a time.

### Multi-Item Carb Calculator (modal)

**Trigger:** Purple `calculate` icon button in the toolbar between "+ Add New Entry" and the Excel export button.

**Tooltip:** *"Meal Carb Calculator — add multiple foods, set quantities and see per-item carbs plus a grand total"*

**Component:** `MealCarbCalculatorModalComponent`

**File:** `client/src/app/features/meal-entry/components/meal-carb-calculator-modal/`

**How it works:**
1. Opens as a `MatDialog` receiving `{ entries: MealEntry[] }` — no extra API call; uses already-loaded history data.
2. **Add Items section:** search box filters the user's food history by name; each result row shows the food name and recorded portion info with a `+` button to add it.
3. **Your Meal section:** each added item shows the food name, an editable quantity input (pre-filled with the original `portionSize`), the unit label, and the calculated carbs for that quantity.
4. **Footer:** running total in grams + a colour-coded CarbLevel badge (green = Low, amber = Medium, red = High).
5. Items can be removed with the `×` button; the total updates live.
6. **Calculator only — nothing is saved.**

**Carb calculation (per item):** same formula as single-item: `(quantity / portionSize) × carbsConsumed`, rounded to 1 dp.

**CarbLevel thresholds (total):** ≤15g → Low, 16–70g → Medium, >70g → High.

**Responsive:** `maxWidth: 95vw`; stacks to single column below 480px; 44px minimum tap targets.

### Feature Service: `MealEntryService`

**File:** `client/src/app/features/meal-entry/services/meal-entry.service.ts`

| Method | HTTP | Endpoint | Notes |
|---|---|---|---|
| `addMealEntry(dto)` | POST | `/mealentry/entries` | Offline fallback via `OfflineQueueService` |
| `getMealEntries(userId?)` | GET | `/mealentry/entries` | Optional userId filter |
| `getMealEntry(id)` | GET | `/mealentry/entries/{id}` | For edit pre-fill |
| `updateMealEntry(id, dto)` | PUT | `/mealentry/entries/{id}` | |
| `deleteMealEntry(id)` | DELETE | `/mealentry/entries/{id}` | |
| `exportToExcel()` | GET | `/export/mealentry-report` | Returns Blob |
| `getWeeklyMealEntries(start?, end?)` | GET | `/mealentry/export/weekly` | Optional date range |

**Offline queue:** On network error (status 0 or 5xx), `addMealEntry()` enqueues to `OfflineQueueService` (type `'meal-entry'`) with label `"{foodItem} – {carbsConsumed}g carbs"`. Replayed by `SyncService` on reconnection.

---

## Data Validation Rules

| Field | Rule |
|---|---|
| `foodItem` | Required, max 200 characters |
| `portionSize` | Required, decimal ≥ 0 |
| `portionUnit` | Required, must be a valid `PortionUnit` enum value |
| `carbsConsumed` | Required, decimal ≥ 0 |
| `carbLevel` | Auto-set by server — not submitted by client |
| CareRecipient edit/delete | 403 Forbidden at server; edit/delete buttons hidden in UI |
| HealthCareProvider edit/delete | 403 Forbidden at server; edit/delete buttons hidden in UI |

---

## End-to-End Data Flow

### Create Entry
```
Carer                       Angular                        API                   DB
  |                         |                               |                     |
  | Navigate /meal-entry/entry|                              |                     |
  | Fill form: Fish, 200g,  |                               |                     |
  | Grams, 30g carbs        |                               |                     |
  |                         |                               |                     |
  | Submit                  |-- POST /mealentry/entries  -->|                     |
  |                         |                               |-- INSERT MealEntry ->|
  |                         |<-- MealEntryResponse ---------|                     |
  |<-- navigate /history    |                               |                     |
```

### Edit Entry
```
Carer                       Angular                        API                   DB
  |                         |                               |                     |
  | Click Edit on row       |                               |                     |
  |<-- navigate /entry?id=5 |                               |                     |
  |                         |-- GET /mealentry/entries/5 -->|                     |
  |                         |<-- MealEntryResponse ---------|                     |
  |<-- form pre-filled      |                               |                     |
  | Change portion to 250g  |                               |                     |
  | Submit                  |-- PUT /mealentry/entries/5 -->|                     |
  |                         |<-- Updated MealEntryResponse -|                     |
  |<-- snackbar + navigate  |                               |                     |
```

### Multi-Item Carb Calculator
```
User                        Angular (modal)                No API call
  |                         |                               |
  | Click purple calc btn   |                               |
  |                         | Opens MealCarbCalculatorModal  |
  |                         | (passes mealEntries already   |
  |                         |  loaded on history page)      |
  | Search "yoghurt"        | filteredEntries getter        |
  | Click + Add             | pushes CarbCalcItem           |
  | Change qty to 150g      | onQuantityChange()            |
  |                         | item.calculatedCarbs =        |
  |                         |   (150/100) × 8.5 = 12.8g    |
  | Add apple (120g/13g)    | totalCarbs = 12.8 + 13 = 25.8g|
  |                         | totalCarbLevel = "Medium"     |
  | Click Close             | dialogRef.close()             |
  |                         | history page unchanged        |
```
