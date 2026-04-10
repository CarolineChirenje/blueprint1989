# Expense Cycles

## Overview

An **ExpenseCycle** represents a bounded time period during which a group of members tracks and shares expenses. Each cycle has a start date, an end date, a list of members, and a status (`Active` or `Closed`). When an Admin closes a cycle, the system calculates each member's net balance and creates `MemberObligation` records showing who owes whom.

Admins create and manage cycles through the Management console. Members can view their active cycle, add expenses, submit payments, and review their obligations.

---

## Role Access

| Role | Access |
|---|---|
| SuperAdmin | Full CRUD on cycles, members, close workflow |
| Admin | Full CRUD on cycles, members, close workflow |
| Member | Read-only: view active cycle, own obligations, own expenses |

---

## Backend

### Controller — `ExpenseCycleController`

**File:** `server/src/Batanai.Api/Controllers/ExpenseCycleController.cs`

Base route: `/api/expense-cycle`

| Method | Route | Auth | Description |
|---|---|---|---|
| GET | `/` | `AllRoles` | Returns all expense cycles |
| GET | `/{id}` | `AllRoles` | Returns a single cycle with members and balances |
| POST | `/` | `AdminOrHigher` | Creates a new expense cycle |
| PUT | `/{id}` | `AdminOrHigher` | Updates name, dates, or member list |
| DELETE | `/{id}` | `AdminOrHigher` | Deletes a cycle (only if no expenses exist) |
| POST | `/{id}/close` | `AdminOrHigher` | Closes the cycle and calculates obligations |
| GET | `/{id}/members` | `AllRoles` | Returns the member list for a cycle |
| POST | `/{id}/members` | `AdminOrHigher` | Add a member to the cycle |
| DELETE | `/{id}/members/{userId}` | `AdminOrHigher` | Remove a member from the cycle |

---

### Model — `ExpenseCycle`

**File:** `server/src/Batanai.Api/Models/ExpenseCycle.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Name` | `string` | e.g., `"January 2026"` |
| `StartDate` | `DateTime` | Cycle start |
| `EndDate` | `DateTime` | Cycle end |
| `Status` | `CycleStatus` | `Active` or `Closed` |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |
| `CycleMembers` | `ICollection<CycleMember>` | Navigation — members of this cycle |
| `Expenses` | `ICollection<Expense>` | Navigation — all expenses in this cycle |
| `MemberObligations` | `ICollection<MemberObligation>` | Navigation — calculated on close |

---

### Model — `CycleMember`

| Field | Type | Notes |
|---|---|---|
| `CycleId` | `int` | PK (composite) — FK ? ExpenseCycle |
| `UserId` | `int` | PK (composite) — FK ? User |
| `JoinedAt` | `DateTime` | |

---

### Model — `MemberObligation`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `CycleId` | `int` | FK ? ExpenseCycle |
| `DebtorUserId` | `int` | FK ? User — who owes |
| `CreditorUserId` | `int` | FK ? User — who is owed |
| `Amount` | `decimal` | Amount owed |
| `IsPaid` | `bool` | Cleared when payment is confirmed |

---

### Service — `ExpenseCycleService`

**File:** `server/src/Batanai.Api/Services/ExpenseCycleService.cs`

#### Key Methods

| Method | Description |
|---|---|
| `GetAllAsync()` | Returns all cycles ordered by start date descending |
| `GetByIdAsync(id)` | Returns a single cycle with members and expense totals |
| `CreateAsync(dto)` | Creates a cycle; validates start = end date |
| `UpdateAsync(id, dto)` | Updates name/dates; blocked if cycle is `Closed` |
| `DeleteAsync(id)` | Deletes a cycle only if it has no expenses |
| `CloseAsync(id)` | Sets status to `Closed`, calculates and saves `MemberObligation` rows |
| `AddMemberAsync(cycleId, userId)` | Adds a `CycleMember` row |
| `RemoveMemberAsync(cycleId, userId)` | Removes a `CycleMember`; blocked if the member has expenses in the cycle |

#### Balance Calculation on Close (`CloseAsync`)

1. Load all expenses for the cycle (amount + payer).
2. Calculate each member's total spend and their equal share (`totalSpend / memberCount`).
3. For each member with a deficit (spend < share), create `MemberObligation` rows to each member with a surplus, using a minimised set of transfers.
4. Persist the `MemberObligation` rows.
5. Send a `CycleCreated`-type notification to all members informing them the cycle has closed and their obligations are ready.

---

### DTOs

#### `CreateExpenseCycleDto`

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | `string` | Yes | Max 100 chars |
| `startDate` | `DateTime` | Yes | |
| `endDate` | `DateTime` | Yes | Must be > startDate |

#### `UpdateExpenseCycleDto`

Same fields as `CreateExpenseCycleDto`, all optional. Blocked when `Status = Closed`.

---

## Frontend

### `CycleManagementComponent`

**File:** `client/src/app/features/management/components/cycle-management.component.ts`

Route: `/management/cycles`

#### Cycle List Table

Columns: Name | Start Date | End Date | Status Badge | Actions (edit, close, delete)

#### Create / Edit Form (Inline)

Fields:
- `name` — required text input.
- `startDate` / `endDate` — date pickers.

#### Close Cycle

"Close" button per Active cycle row. Opens a confirmation dialog explaining that closing is irreversible and will calculate member obligations. On confirm, calls `POST /expense-cycle/{id}/close`.

#### Delete

Confirmation dialog. If the cycle has expenses, the API returns 400 and the component shows: "Cannot delete a cycle with existing expenses."

---

### `ExpenseCycleService` (Angular)

**File:** `client/src/app/features/cycles/services/expense-cycle.service.ts`

| Method | HTTP | Endpoint |
|---|---|---|
| `getAll()` | GET | `/api/expense-cycle` |
| `getById(id)` | GET | `/api/expense-cycle/{id}` |
| `create(dto)` | POST | `/api/expense-cycle` |
| `update(id, dto)` | PUT | `/api/expense-cycle/{id}` |
| `delete(id)` | DELETE | `/api/expense-cycle/{id}` |
| `close(id)` | POST | `/api/expense-cycle/{id}/close` |
| `getMembers(id)` | GET | `/api/expense-cycle/{id}/members` |
| `addMember(id, userId)` | POST | `/api/expense-cycle/{id}/members` |
| `removeMember(id, userId)` | DELETE | `/api/expense-cycle/{id}/members/{userId}` |

---

## End-to-End Cycle Flow

```
Admin creates "January 2026" cycle via /management/cycles
  ? POST /api/expense-cycle { name: "January 2026", startDate: ..., endDate: ... }
  ? Cycle created with status Active

Admin adds members
  ? POST /api/expense-cycle/{id}/members for each user

Members add expenses throughout the month
  ? POST /api/expense { cycleId, amount, category, description }

At end of month, Admin closes the cycle
  ? POST /api/expense-cycle/{id}/close
  ? System calculates each member's share and creates MemberObligation rows
  ? Members receive push notification: "January 2026 cycle closed. View your obligations."

Members settle obligations
  ? POST /api/payment { obligationId, amount }
  ? Admin or creditor confirms: PATCH /api/payment/{id}/confirm
  ? MemberObligation.IsPaid set to true
```
---

## Mukando Cycles (Rotating Savings)

A **Mukando** cycle is a rotating savings group (stokvel). Members contribute a fixed amount each round, and the full pool is paid out to one recipient on rotation. Batanai fully manages:

- Round generation based on configured frequency and payout order
- Contribution tracking with proof-of-payment uploads
- Payout recording and verification
- Swap and opt-out requests
- Automated push notification reminders

---

### Mukando Contribution Status Flow

```
Pending → Paid → AwaitingVerification → Confirmed
                                      ↓
                                  (rejected) → Paid (reset)
       → Missed (force-close)
```

| Status | Meaning |
|---|---|
| `Pending` | Member has not yet submitted a contribution for this round |
| `Paid` | Member uploaded proof; awaiting admin confirmation |
| `AwaitingVerification` | Admin initiated confirmation; awaiting independent verifier |
| `Confirmed` | Contribution verified and accepted; amount added to pool |
| `Missed` | Round was force-closed without contribution |

---

### Two-Step Verification System

#### Purpose

To prevent single-admin fraud — a group admin cannot unilaterally confirm their own contribution or record a payout to themselves. Every confirmation flows through a cryptographically-random, independent verifier selected from the cycle's active participants.

#### How It Works

1. **Admin confirms a contribution** → system creates a `MukandoVerificationRequest` and randomly assigns a verifier from cycle members (excluding the contributor, admin, and round recipient)
2. **Verifier receives a push notification** with details of what to verify
3. **Verifier approves or rejects** via the Rounds tab → verification banner
4. On approval: contribution status → `Confirmed`, amount added to `ActualCollected`
5. On rejection: contribution status reverts to `Paid` (re-submittable)

#### Admin-Pays Special Cases

| Scenario | Behaviour |
|---|---|
| Contributor is a GroupAdmin | Contribution goes directly to `AwaitingVerification` on upload — no admin confirmation step needed |
| Admin tries to confirm own contribution | API returns 400: "You cannot confirm your own contribution" |
| Admin tries to record payout to themselves (they are the round recipient) | API returns 400: "You cannot record a payout to yourself" |
| Round recipient is a GroupAdmin | Verifier is selected exclusively from non-admin members to prevent collusion |

#### Verifier Identity Protection

While a verification is in `Pending` state, the assigned verifier's identity is hidden from the response (name = "Pending", id = 0). This prevents admins from pressuring the verifier before they respond.

#### Verifier Reassignment

Admins can reassign a verification to a different random member (e.g. if the current verifier is unavailable). The previous request is marked `Reassigned` and a new request is created, excluding the old verifier.

Verifications expire after **48 hours**. Expired verifications can be reassigned by admins.

---

### Mukando Payout Verification

Recording a payout also triggers the two-step verification system:

1. Admin fills in payout amount, method, reference, and proof → submits
2. System stores the pending payout details in `MukandoVerificationRequest` (no `MukandoPayout` row created yet)
3. A random verifier is assigned and notified
4. On approval: `MukandoPayout` is created, round status → `Completed`, next round activates (or cycle completes)
5. On rejection: admin is notified; no payout is recorded; admin can submit again

While a payout verification is pending, the admin's "Record Payout" form is replaced with an "Awaiting independent verification" notice in the UI.

### UI — Verification Banner (Rounds Tab)

A yellow banner appears at the top of the Rounds tab whenever the current user has a pending verification assignment. The banner shows:

- Who/what they are verifying (contributor name + amount, or payout round)
- An expiry date
- A rejection reason textarea (required for rejection)
- Approve and Reject action buttons

All verification UI is fully responsive across mobile, tablet, and desktop layouts.
