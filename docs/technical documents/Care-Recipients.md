# Care Recipients & Carer Linking

## Overview

The Care Recipient Linking feature manages the many-to-many relationships between care-providing staff (Carer, SupportWorker, HealthCareProvider) and the care recipients they support. A carer can link to a care recipient by email address, granting access to that person's health records. Care recipients can initiate a delink request to remove a carer from their care team, which an Administrator must approve or reject. Admins can directly manage all carer-CR associations and view or remove links from the Management section.

---

## Role Access

| Action | SuperAdmin | Administrator | Carer | SupportWorker | HealthCareProvider | CareRecipient |
|---|---|---|---|---|---|---|
| Link self to CR (by email) | — | — | ✓ | ✓ | ✓ | — |
| Remove self from CR | — | — | ✓ | ✓ | ✓ | — |
| Submit delink request | — | — | — | — | — | ✓ |
| View own linked CRs/carers | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Admin: view all CRs | ✓ | ✓ | — | — | — | — |
| Admin: remove any carer from CR | ✓ | ✓ | — | — | — | — |
| Approve/reject delink requests | ✓ | ✓ | — | — | — | — |

---

## Backend

### Controller: `CareRecipientController` — `/api/carerecipient`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| GET | `/api/carerecipient` | Authorized | Staff: linked CRs; CareRecipient: linked carers |
| POST | `/api/carerecipient` | Carer/SupportWorker/HCP only | Link self to a CR by email |
| DELETE | `/api/carerecipient/{careRecipientId}` | Authorized | Staff removes themselves from a CR |
| GET | `/api/carerecipient/all-active` | AdminOrHigher | All active CRs with conditions |
| GET | `/api/carerecipient/all` | AdminOrHigher | All CRs including inactive |
| GET | `/api/carerecipient/admin/{id}/linked-carers` | AdminOrHigher | All carers linked to a specific CR |
| DELETE | `/api/carerecipient/admin/{id}/linked-carer/{carerId}` | AdminOrHigher | Admin removes a specific carer link |

### Business Logic

**POST `/api/carerecipient` — Link by Email**
1. Validates the requester is Carer, SupportWorker, or HealthCareProvider (not CR or Admin).
2. Looks up the target user by email; validates they are an active `CareRecipient`.
3. Checks that the link does not already exist.
4. Creates `UserCareRecipient` row: `{ UserId: requesterId, CareRecipientId: targetId, LinkedAt: now }`.
5. Sends a `CareRecipientLinked` push notification to the CR.

**DELETE `/api/carerecipient/{careRecipientId}` — Staff self-remove**
1. Deletes the `UserCareRecipient` row for `(requesterId, careRecipientId)`.
2. Sends push notification to the care recipient informing them of the removal.

**DELETE `/api/carerecipient/admin/{id}/linked-carer/{carerId}`**
1. Admin hard-deletes the specific `UserCareRecipient` row.
2. Sends `CarerRemoved` push to the carer.
3. Sends `CareRecipientRemoved` push to the care recipient.

**GET `/api/carerecipient`** — returns:
- For Carer/SupportWorker/HCP: a `CareRecipientDto[]` list with name, email, DOB, conditions, diabetes type, HBP flag, yearDiagnosed.
- For CareRecipient: a `CarerDto[]` list with name, email, role.
- For Admin: use `/all-active` or `/all` instead.

### Key DTOs

**`AddCareRecipientByEmailRequest`**
```
email    string    Email of the CareRecipient to link to
```

**`CareRecipientDto`** (staff's view of a CR)
```
id                 int
firstName          string
lastName           string
email              string
dateOfBirth        DateTime?
yearDiagnosed      int?
diabetesType       string?    "Type 1" | "Type 2" | null
hasHbp             bool
conditions         string[]   Condition names
isActive           bool
```

**`CarerDto`** (CR's view of their carer)
```
id         int
firstName  string
lastName   string
email      string
role       string
```

### Model: `UserCareRecipient`

| Field | Type | Notes |
|---|---|---|
| `UserId` | int | FK → User (Carer/SupportWorker/HCP) |
| `CareRecipientId` | int | FK → User (CareRecipient) |
| `LinkedAt` | DateTime | |

Composite PK: `(UserId, CareRecipientId)`.

### `CareRecipientService`

**File:** `server/src/Vitara.Api/Services/CareRecipientService.cs`

Key methods:
- `GetLinkedCareRecipientIdsAsync(userId)` — returns list of CR IDs for a given carer.
- `AddCareRecipientByEmailAsync(userId, email)` — full add flow as described above.
- `RemoveCareRecipientAsync(userId, careRecipientId)` — delete link + notify CR.
- `GetCarersForCareRecipientAsync(careRecipientId)` — full list of linked staff for a CR.
- `GetCarerUserIdsForCareRecipientAsync(careRecipientId)` — returns just User IDs for bulk push notification targeting.
- `GetLinkedCareRecipientsAsync(userId)` — full DTOs with condition/type enrichment.

---

## Delink Request Workflow

### Controller: `DelinkRequestController` — `/api/delinkrequest`

| Method | Route | Auth Policy | Description |
|---|---|---|---|
| POST | `/api/delinkrequest` | CareRecipient only | Submit a delink request |
| GET | `/api/delinkrequest` | AdminOrHigher | List all pending delink requests |
| POST | `/api/delinkrequest/{id}/approve` | AdminOrHigher | Approve — removes the carer link |
| POST | `/api/delinkrequest/{id}/reject` | AdminOrHigher | Reject — notifies the care recipient |

**POST `/api/delinkrequest`**
- Creates a `DelinkRequest` row with `Status = Pending`, `RequestedAt = now`.
- Only CareRecipient role can submit.

**POST `/api/delinkrequest/{id}/approve`**
1. Sets `DelinkRequest.Status = Approved`, `ProcessedAt = now`.
2. Calls `CareRecipientService.RemoveCareRecipientAsync()` to delete the `UserCareRecipient` link.
3. Sends `DelinkApproved` push (type 7) to the care recipient.

**POST `/api/delinkrequest/{id}/reject`**
1. Sets `DelinkRequest.Status = Rejected`, `ProcessedAt = now`.
2. Sends `DelinkDenied` push (type 8) to the care recipient.

**`CreateDelinkRequestDto`**
```
carerId    int    The User ID of the carer to request removal of
```

### Model: `DelinkRequest`

| Field | Type | Notes |
|---|---|---|
| `Id` | int | PK |
| `CareRecipientId` | int | FK → User |
| `CarerId` | int | FK → User |
| `Status` | DelinkStatus | Pending / Approved / Rejected |
| `RequestedAt` | DateTime | |
| `ProcessedAt` | DateTime? | |

---

## Frontend

### Profile → Care Recipients Page

**File:** `client/src/app/features/profile/components/care-recipients.component.ts`
**Route:** `/profile/care-recipients`

#### Carer / SupportWorker / HealthCareProvider View

**List:** Shows linked care recipients as cards or table rows:
- Name, email, age (computed from DOB), year diagnosed, diabetes type badge.
- Conditions chips (T1D, T2D, HBP).

**Add CR:**
- Text input for email address.
- Calls `CareRecipientService.addCareRecipient(email)` → `POST /carerecipient`.
- On success: refreshes list, shows snackbar.
- On error (email not found, already linked): shows inline error message.

**Remove CR:**
- Confirmation dialog via `DialogService.confirmDelete()`.
- Calls `CareRecipientService.removeCareRecipient(id)` → `DELETE /carerecipient/{id}`.

**Search:** Client-side filter on name and email.

#### CareRecipient View

**List:** Shows linked carers and support workers:
- Name, email, role badge.

**Delink Request:**
- "Request Removal" button per carer.
- Calls `DelinkRequestService.createDelinkRequest(carerId)` → `POST /delink-requests`.
- Button changes to "Pending" badge while the request awaits admin decision.
- `pendingDelinkCarerIds` Set tracks which requests are in-flight.

---

### Management → Care Recipients Page (`AdminOnly`)

**File:** `client/src/app/features/management/components/care-recipient-management.component.ts`
**Route:** `/management/care-recipients`

- Lists **all** care recipients (active + inactive) via `GET /carerecipient/admin/all`.
- Client-side search; "Find by email" convenience field.
- Shows age (computed from DOB), diagnosis year with duration.
- **Expandable rows:** Calls `GET /carerecipient/admin/{id}/linked-carers` on expand to load the carer list for that CR.
- **Admin delink:** "Remove" button in the expanded carer list calls `DELETE /carerecipient/admin/{id}/linked-carer/{carerId}`. Confirmation dialog required.

---

### Management → Delink Requests Page (`AdminOnly`)

**File:** `client/src/app/features/management/components/delink-request-management.component.ts`
**Route:** `/management/delink-requests`

- Lists pending delink requests via `GET /delink-requests/pending`.
- Each row shows: Care Recipient name, Carer name, Requested At.
- **Approve button:** `POST /delink-requests/{id}/approve`. Shows per-row spinner while in-flight.
- **Reject button:** `POST /delink-requests/{id}/reject`. Shows per-row spinner while in-flight.
- Page auto-refreshes after each action.

---

## Push Notifications

| Trigger | Type | Recipient |
|---|---|---|
| Staff links to CR | `General` (1) | CareRecipient |
| Staff removes themselves from CR | `CareRecipientRemoved` (5) | CareRecipient |
| Admin removes carer | `CarerRemoved` (4) | Carer |
| Admin removes carer | `CareRecipientRemoved` (5) | CareRecipient |
| Delink request approved | `DelinkApproved` (7) | CareRecipient |
| Delink request rejected | `DelinkDenied` (8) | CareRecipient |

---

## End-to-End Data Flows

### Carer Links to CR
```
Carer                       Angular                 API                   DB
  |                         |                       |                     |
  | Enter email: jane@...   |                       |                     |
  | Click "Add"             |-- POST /carerecipient->|                     |
  |                         |                       |-- lookup user by email
  |                         |                       |-- INSERT UserCareRecipient>|
  |                         |                       |-- push to Jane       |
  |                         |<-- CareRecipientDto --|                     |
  | CR added to list        |                       |                     |
```

### CR Submits Delink Request → Admin Approves
```
CareRecipient               Angular                 API                   DB
  |                         |                       |                     |
  | Click "Request Removal" |-- POST /delink-requests>|                   |
  | of John (carer)         |                       |-- INSERT DelinkRequest>|
  |                         |<-- 201 Created -------|                     |
  | Button → "Pending"      |                       |                     |
  |                         |                       |                     |
  (Admin reviews)           |                       |                     |
  |                         |-- POST .../approve -->|                     |
  |                         |                       |-- UPDATE status=Approved
  |                         |                       |-- DELETE UserCareRecipient
  |                         |                       |-- push DelinkApproved to CR
  |<-- push: "Carer removed"|                       |                     |
```
