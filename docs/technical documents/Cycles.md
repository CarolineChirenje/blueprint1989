# Expense Cycles

## Overview

An **ExpenseCycle** represents a bounded time period during which a group of members tracks and shares expenses. Each cycle has a start date, an end date, a list of members, and a status (`Draft`, `Active`, or `Closed`). Cycles begin in `Draft` while setup is in progress (members, agreements, Mukando configuration). An Admin starts the cycle once all readiness checks pass, moving it to `Active`. When an Admin closes a cycle, the system calculates each member's net balance and creates `MemberObligation` records showing who owes whom.

Batanai supports two cycle types:

- **Majana** — shared expense tracking. Members add expenses; the system calculates each Participant's equal share; at close, obligations are generated and members settle up.
- **Mukando** — rotating savings (stokvel). Members contribute a fixed amount each round; the full pool is paid to one recipient on rotation per the agreed payout order.

Admins create and manage cycles through the Management console. Members can view their cycle, add expenses or contributions, submit payments, and review their obligations.

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
| `GroupId` | `int` | FK → Group |
| `CurrencyId` | `int` | FK → Currency |
| `CycleType` | `CycleType` | `Majana` or `Mukando` |
| `StartDate` | `DateTime` | Cycle start |
| `EndDate` | `DateTime` | Cycle end |
| `Status` | `CycleStatus` | `Draft`, `Active`, or `Closed` |
| `ContributionAmount` | `decimal?` | Mukando only — fixed amount per round |
| `Frequency` | `CycleFrequency?` | Mukando only — `Weekly`, `Fortnightly`, `Monthly` |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |
| `CycleMembers` | `ICollection<CycleMember>` | Navigation — members of this cycle |
| `Expenses` | `ICollection<Expense>` | Navigation — all expenses in this cycle |
| `MemberObligations` | `ICollection<MemberObligation>` | Navigation — calculated on close |

---

### Model — `CycleMember`

| Field | Type | Notes |
|---|---|---|
| `ExpenseCycleId` | `int` | PK (composite) — FK → ExpenseCycle |
| `UserId` | `int` | PK (composite) — FK → User |
| `CycleRole` | `CycleRole` | `Participant` or `Observer` |
| `AddedAt` | `DateTime` | When the member was added to the cycle |

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

---

## KYC Verification (Mukando Only)

To reduce risk in Mukando cycles, **identity verification (KYC)** is required for Mukando participation only. It is **not required for Majana** cycles.

### Why KYC is needed

In a Mukando round, one person receives the full pool. That creates a higher trust and follow-up requirement than normal expense sharing. KYC gives the group a practical way to confirm who a person is before the cycle starts.

### KYC is account-level, not cycle-level

A user completes KYC **once per account**. Once approved, they can join any future Mukando cycle without resubmitting documents.

### KYC Statuses

| Status | Meaning |
|---|---|
| `NotStarted` | The user has not submitted any KYC details yet |
| `PendingReview` | The user submitted KYC details and is waiting for admin review |
| `Verified` | An admin approved the KYC details |
| `Rejected` | An admin rejected the submission; the user can fix issues and resubmit |
| `AdminBypassed` | An admin manually vouched for the user under special circumstances |

### User KYC Flow

1. User opens **Profile** → **KYC Verification**
2. User selects an ID type: `NationalId`, `Passport`, `DriversLicense`, or `NoDocument`
3. User enters their full name as it appears on the document
4. If they have a document, they upload:
   - a photo/scan of the document
   - optionally a **selfie holding the ID** next to their face
5. Submission is stored and platform admins are notified for review

### No Document Path

A user who has no ID is **not blocked from using the whole system**. Instead:

- They can still sign up and join groups
- They can still participate in Majana cycles
- For Mukando, they select **`NoDocument`** and ask a trusted admin to manually review and bypass

### Admin Review Flow

Admins can open the KYC review queue and:

- view the submitted document image
- view the selfie-with-ID image (if provided)
- approve the user
- reject the user with a reason
- manually bypass the requirement with a required note

### Admin Bypass

Admin bypass exists for situations such as:

- the admin personally knows the member
- the member does not currently have an identity document
- community or informal groups where trust is based on local knowledge

When using bypass, the admin must record a note so there is an audit trail of who vouched for the person and why.

### Enforcement Gates

KYC is enforced in **three places** for Mukando:

1. **Add member to Mukando cycle** — user must be `Verified` or `AdminBypassed`
2. **Start Mukando cycle** — the cycle cannot start until **all members** are `Verified` or `AdminBypassed`
3. **Record payout** — payout recording checks KYC again as a final safeguard

This means a Mukando cycle stays in **Draft** until all members are KYC-ready.

### UI Behaviour

- The cycle detail page shows a **KYC readiness panel** in Draft mode for Mukando cycles
- Admins can see exactly which members are blocking the cycle start
- The **Start Cycle** action is disabled until all members are verified or bypassed
- All KYC UI is responsive across **mobile, tablet, and desktop** layouts

### File Privacy

KYC files are **not treated like normal public proof files**. If an uploaded file is linked to a KYC record, access is restricted to:

- the file owner, or
- an Admin / SuperAdmin

Anonymous access to KYC identity documents is blocked.

---

## Member Agreement System

### Overview

Before a cycle can be started, **every participating member must explicitly record their agreement**. This creates a timestamped, auditable trail of consent — ignorance cannot be used as a defence during disputes. The feature applies to both **Majana** and **Mukando** cycle types.

### Data Model

**`CycleMemberAgreements` table**

| Column | Type | Description |
|---|---|---|
| `Id` | int (PK) | Auto-increment |
| `ExpenseCycleId` | int (FK → ExpenseCycles) | Which cycle |
| `UserId` | int (FK → Users) | Which member agreed |
| `AgreedAt` | DateTime UTC | Timestamp of agreement |

A unique constraint on `(ExpenseCycleId, UserId)` ensures each member can agree only once per cycle instance (before any reset).

### API Endpoints

| Method | Route | Auth | Description |
|---|---|---|---|
| `GET` | `/api/cycles/{id}/agreements` | Any member or admin | Returns agreement status for all members |
| `POST` | `/api/cycles/{id}/agree` | Any cycle member | Records the calling user's agreement |

**`GET /agreements` response shape:**
```json
{
  "allAgreed": false,
  "agreedCount": 2,
  "totalCount": 3,
  "members": [
    { "userId": 1, "userName": "Alice Smith", "hasAgreed": true, "agreedAt": "2026-04-20T10:30:00Z" },
    { "userId": 2, "userName": "Bob Jones",  "hasAgreed": true, "agreedAt": "2026-04-20T11:00:00Z" },
    { "userId": 3, "userName": "Carol Dube", "hasAgreed": false, "agreedAt": null }
  ]
}
```

### Agreement Reset Rules

All existing agreements are **automatically deleted** (and all members notified via push) whenever any of the following changes occur to a Draft cycle:

1. **Cycle settings updated** — name, start/end date (`UpdateAsync`)
2. **Member added** — single or batch add (`AddMemberAsync`, `AddMembersBatchAsync`)
3. **Member removed** (`RemoveMemberAsync`)
4. **Opt-out request approved** — member leaves the cycle (`RespondOptOutRequestAsync`)
5. **Mukando settings updated** — contribution amount, frequency, payout order (`MukandoService.UpdateSettingsAsync`)

Agreements are **not** reset when:
- A dispute is raised or resolved
- A swap request is created, responded to, or cancelled (swaps don't change cycle terms)

### Start Cycle Validation Order

`StartAsync` checks the following conditions in order and blocks the start with a descriptive error if any fail:

1. Cycle must be in `Draft` status
2. At least 2 members required
3. **No pending swap requests** (Mukando only — previously auto-cancelled, now blocks)
4. **No pending opt-out requests**
5. **No open disputes** (Pending or Reviewed status — applies to both Majana and Mukando)
6. **All members must have agreed**
7. Mukando: rounds must exist (payout order must be set)

### Notifications

| Notification Type | Sent to | Trigger |
|---|---|---|
| `CycleAgreementsReset` (40) | All current members | Any reset event above |
| `CycleAllMembersAgreed` (41) | Cycle admins (Group Admins) | Last member submits their agreement |

### UI — Admin (Setup Checklist)

The **Setup Checklist** card (visible to Group Admins on Draft cycles) gained three new check items:

- **No pending swap requests** (Mukando only) — shows pending count with "Resolve" button linking to Swaps tab
- **No pending opt-out requests** — shows pending count with "Resolve" button linking to Opt-outs tab
- **No open disputes** — shows open count with "Resolve" button linking to Disputes tab
- **All members agreed** — shows `{agreed}/{total}` count with "View" button linking to Agreements tab

### UI — Members (Readiness Card)

A **Readiness Card** is displayed to non-admin cycle members on Draft cycles. It shows:

- Whether all opt-out requests are resolved
- Whether all disputes are resolved
- How many members have agreed (`{n}/{total}`)
- If the current user has not yet agreed: an **"I Agree — Record my Agreement"** button
- If the current user has already agreed: a confirmation chip with the exact timestamp

### UI — Agreements Tab

A new **Agreements** tab is available on all Draft cycles (admin and members alike). It displays:

- A summary strip showing total agreed count and the user's own agreement status
- A card for each member, colour-coded green when agreed, with the exact timestamp
- The "I Agree" button if the current user has not yet agreed

The badge on the Agreements tab button shows `{agreed}/{total}` and turns green when all members have agreed.

### UI — Start Cycle Tooltip

The **Start Cycle** button tooltip now cycles through blocking conditions in priority order:
1. Unresolved disputes
2. Pending opt-out requests
3. Members who haven't agreed
4. Fewer than 2 members

### Scenarios

| Scenario | Outcome |
|---|---|
| Admin adds a member → existing agreements wiped | All members (including previous agreers) are notified to re-agree |
| Admin updates cycle dates → agreements wiped | Push notification sent; everyone must re-agree |
| Member opts out and admin approves → member removed, agreements wiped | Remaining members re-agree |
| Admin tries to start before all agree | `400 Bad Request`: "X/Y agreed" — blocked |
| Admin tries to start with a pending swap request | `400 Bad Request`: blocked |
| Admin tries to start with a pending opt-out request | `400 Bad Request`: blocked |
| Last member clicks "I Agree" | Admins receive a `CycleAllMembersAgreed` push notification |

### Responsiveness

All agreement and readiness UI components use `flex-wrap: wrap` and collapse to a single column at `max-width: 600px`, ensuring correct display on mobile, tablet, and desktop.

---

## Observer Members

### What is an Observer?

A cycle member can be added with the role `CycleRole.Observer` instead of the default `CycleRole.Participant`. Observers are full members of the cycle for visibility and notification purposes, but carry **no financial obligations**.

### Observer Capabilities

| Action | Observer | Participant |
|---|---|---|
| View cycle details and tabs | ✓ | ✓ |
| Receive push notifications | ✓ | ✓ |
| Raise disputes | ✓ | ✓ |
| Submit opt-out requests | ✓ | ✓ |
| Must sign agreement before cycle start | ✓ | ✓ |
| Owes a share of expenses (Majana) | ✗ | ✓ |
| Assigned a Mukando round slot | ✗ | ✓ |
| Counted in per-member share calculation | ✗ | ✓ |

### Adding an Observer

Both single-member and batch-member add endpoints accept an optional `cycleRole` parameter:

- `POST /api/cycles/{id}/members` — body: `{ "userId": 5, "cycleRole": "Observer" }`
- `POST /api/cycles/{id}/members/batch` — body: `{ "userIds": [5, 6], "cycleRole": "Observer" }`

When a member is added as an Observer, they receive a distinct push notification clarifying they have no financial obligations:

> *"You have been added as an Observer to the {cycle.Name} cycle in {groupName}. You will receive notifications but have no financial obligations."*

Observers cannot be added to an **Active** or **Closed** cycle (same restriction as Participants).

### Observer Exclusion from Financial Logic

Three service-level guards enforce the Observer boundary:

1. **Share calculation** (`GetOutstandingSummaryAsync`) — the Majana per-member share query (`Q2`) only counts `CycleRole.Participant` members. This prevents the "diluted share" bug that occurred before the `CycleRole` column was introduced.
2. **Mukando round generation** (`RegenerateRoundsAfterMemberChangeAsync`) — adding a Participant regenerates rounds; adding an Observer does not, because Observers are never assigned a payout slot.
3. **Opt-out financial cleanup** (`RespondOptOutRequestAsync`) — when an Observer's opt-out is approved, the code checks `isObserver = member.CycleRole == CycleRole.Observer` and skips both `RecalculateAllObligationsAsync` (Majana) and `RegenerateRoundsAfterMemberChangeAsync` (Mukando), because the Observer had no financial records to clean up.

### Agreements and Observers

Observers **are** required to agree before a cycle can start. They appear in the Agreements tab alongside Participants and are included in the `{agreed}/{total}` count. The rationale is that an Observer is still a **cycle member** who should acknowledge the terms even without financial obligations.

---

## Cycle Lifecycle & Status Workflow

### Status Values

| Status | Meaning |
|---|---|
| `Draft` | Setup phase — members being added, agreements being collected, payout order being configured |
| `Active` | Live — expenses (Majana) or contributions and rounds (Mukando) are in progress |
| `Closed` | Finished — obligations calculated (Majana) or all rounds complete (Mukando) |

### State Machine

```
Draft ──(StartAsync)──► Active ──(CloseAsync)──► Closed
```

A cycle cannot move backwards. There are no other transitions.

### Draft Phase

- Admin creates the cycle via `POST /api/cycles` — always starts `Draft`.
- Admin adds members (Participant or Observer).
- For Mukando: admin configures contribution amount, frequency, and payout order.
- For Mukando: all members must complete KYC (`Verified` or `AdminBypassed`).
- All members review the terms and record their agreement.
- Admin uses the **Setup Checklist** on the cycle detail page to track readiness.

### StartAsync — Validation Order

`StartAsync` performs seven ordered checks and blocks with a descriptive error if any fail:

| # | Check | Applies to |
|---|---|---|
| 1 | Cycle must be in `Draft` status | Both |
| 2 | At least 2 members required | Both |
| 3 | No pending swap requests | Mukando only |
| 4 | No pending opt-out requests | Both |
| 5 | No open disputes (`Pending` or `Reviewed`) | Both |
| 6 | All members must have agreed | Both |
| 7 | Rounds must exist (payout order set) | Mukando only |

On success the cycle status is set to `Active` and a push notification is sent to all members.

### Active Phase

- **Majana**: members submit expenses; payments are recorded; disputes can be raised and resolved.
- **Mukando**: rounds activate in sequence; members submit contributions; the round recipient is paid and a verifier confirms the payout; the next round begins automatically.
- No new members can be added once a cycle is `Active`.

### CloseAsync

Closing is only possible while the cycle is `Active`.

**Majana close:**
1. Calculate each Participant's equal share of total expenses.
2. Generate `MemberObligation` rows (minimised transfer set).
3. Set status to `Closed`.
4. Notify all members: "Cycle closed — view your obligations."

**Mukando close:**
Mukando cycles close automatically when the final round completes (last payout verified). Manual early close is also available for admins in exceptional circumstances.

---

## Dashboard — Outstanding Summary API

### Endpoint

`GET /api/cycles/outstanding-summary`

No parameters. Returns a summary scoped to the **authenticated user's** active and draft cycles.

### Response Shape

```json
{
  "totalOutstanding": 45.00,
  "totalIncoming":    120.00,
  "cycleCount":       3,
  "cycles": [
    { ... }
  ]
}
```

**`OutstandingSummaryDto`**

| Field | Type | Description |
|---|---|---|
| `totalOutstanding` | `decimal` | Sum of all `outstanding` values across cycles |
| `totalIncoming` | `decimal` | Sum of all `expectedPayout` values (Mukando recipients only) |
| `cycleCount` | `int` | Number of cycles included in the response |
| `cycles` | `CycleOutstandingItemDto[]` | Per-cycle breakdown |

**`CycleOutstandingItemDto`**

| Field | Type | Description |
|---|---|---|
| `cycleId` | `int` | |
| `cycleName` | `string` | |
| `groupId` | `int` | |
| `groupName` | `string` | |
| `cycleType` | `string` | `"Majana"` or `"Mukando"` |
| `cycleStatus` | `string` | `"Draft"`, `"Active"` |
| `currencySymbol` | `string` | e.g. `"$"` |
| `outstanding` | `decimal` | Amount user still owes; `0` for recipients and Observers |
| `sharePerMember` | `decimal?` | Majana only — equal share of total expenses |
| `totalPaid` | `decimal?` | Majana only — user's confirmed payments so far |
| `activeRoundNumber` | `int?` | Mukando only — current active round number |
| `contributionDueDate` | `DateTime?` | Mukando only — round end date |
| `contributionStatus` | `string?` | Mukando only — `"Pending"`, `"Paid"`, `"AwaitingVerification"`, `"Confirmed"`, `"Recipient"` |
| `pendingAgreement` | `bool` | `true` if this is a Draft cycle and the user has not yet agreed |
| `expectedPayout` | `decimal?` | Mukando recipients only — `round.ExpectedPool` for the current round |

### Recipient Rows

When the authenticated user is the **round recipient** for the current active Mukando round:

- `contributionStatus` = `"Recipient"`
- `outstanding` = `0`
- `expectedPayout` = `round.ExpectedPool` (the total pool amount they will receive)

This value feeds the **Total Incoming** figure displayed on the dashboard hero card.

### Observer Rows

Observers appear in the summary with `outstanding = 0`. For Majana cycles, they are excluded from the participant count used in the share calculation, so the share shown to Participants is not diluted.

### Performance

The method uses **7 fixed database queries** regardless of how many cycles the user belongs to — replacing an earlier N+1 pattern where one query ran per cycle:

| Query | Purpose |
|---|---|
| Q1 | All active/draft cycle memberships + group name + currency symbol |
| Q2 | Participant counts per active Majana cycle (Observers excluded) |
| Q3 | Total expenses per active Majana cycle |
| Q4 | User's confirmed payments per active Majana cycle |
| Q5 | Active rounds per active Mukando cycle |
| Q6 | User's contributions in those active rounds |
| Q7 | Draft cycles the user has already agreed to |

### Dashboard UI States

The dashboard hero card has three display states based on the summary:

| State | Condition |
|---|---|
| Outstanding amounts shown | `totalOutstanding > 0` |
| No payments due + incoming payout | `totalOutstanding = 0 && totalIncoming > 0` |
| Fully settled | `totalOutstanding = 0 && totalIncoming = 0` |

Draft cycles with a pending agreement surface a distinct badge on their cycle row regardless of the financial state.

---

## Push Notification Deep Links

All cycle-related push notifications include a deep link that routes the user directly to the relevant tab in the cycle detail page, using the `?tab=` query parameter.

| Event | Tab |
|---|---|
| Contribution due reminder | `?tab=rounds` |
| Contribution confirmed / receipt issued | `?tab=rounds` |
| Payout recorded | `?tab=rounds` |
| Swap request created or responded to | `?tab=swaps` |
| Opt-out request created or responded to | `?tab=optouts` |
| Dispute raised or resolved | `?tab=disputes` |
| Agreements reset | `?tab=agreements` |
| All members agreed notification | `?tab=agreements` |

The tab routing is handled by `cycle-detail.component.ts` which reads `route.queryParamMap.get('tab')` on load and selects the matching tab.

Valid tab values: `expenses`, `payments`, `summary`, `disputes`, `members`, `rounds`, `stats`, `activity`, `swaps`, `optouts`, `order`, `agreements`.
