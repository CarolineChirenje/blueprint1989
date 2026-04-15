# KYC — Know Your Customer

## Overview

KYC (Know Your Customer) is an identity-verification gate that all Mukando cycle members must pass before they can participate. It is **not** required for Majana (expense-sharing) cycles.

The goal is lightweight fraud deterrence at zero cost: users submit their full legal name, an optional government-issued document photo, and an optional selfie. An admin reviews the submission and either approves or rejects it. Admins can also bypass the check entirely when they already know the person.

---

## Statuses

`KycStatus` lives on the `User` entity and drives all enforcement logic.

| Value | Int | Meaning |
|---|---|---|
| `NotStarted` | 0 | Default. User has never submitted. |
| `PendingReview` | 1 | Submission received; awaiting admin review. |
| `Verified` | 2 | Admin approved — user can join Mukando cycles. |
| `Rejected` | 3 | Admin rejected — user must resubmit. |
| `AdminBypassed` | 4 | Admin manually cleared the user without a document review. |

`Verified` and `AdminBypassed` are the two states that satisfy all KYC gates.

---

## ID Types

| Value | Int |
|---|---|
| `NationalId` | 0 |
| `Passport` | 1 |
| `DriversLicense` | 2 |
| `NoDocument` | 3 |

When `NoDocument` is selected, `IdNumber` and the document photo are both optional. The admin still reviews the submission and decides whether to approve based on the name and selfie alone (or a side-channel trust).

---

## Data Model

### `User` (modified fields)

| Field | Type | Notes |
|---|---|---|
| `PhoneNumber` | `string?` | Max 30 chars. Set at signup or via profile update. |
| `KycStatus` | `KycStatus` | Default `NotStarted`. Updated whenever a review/bypass is recorded. |

### `UserKycDocument`

**File:** `server/src/Batanai.Api/Models/UserKycDocument.cs`

A user can have multiple records — each rejected submission is retained for audit. Only the latest record drives `User.KycStatus`.

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `UserId` | `int` | FK → `Users` (cascade delete) |
| `IdType` | `KycIdType` | Enum — document type selected |
| `IdNumber` | `string?` | ID number as printed. Null for `NoDocument`. |
| `FullNameOnId` | `string` | Legal name. Required for all paths. |
| `DocumentFileId` | `int?` | FK → `UploadedFiles` (set null on delete). Photo of document. |
| `SelfieWithIdFileId` | `int?` | FK → `UploadedFiles` (set null on delete). Selfie holding document. |
| `Status` | `KycStatus` | Current state of this submission record. |
| `SubmittedAt` | `DateTime` | UTC timestamp of submission. |
| `ReviewedByUserId` | `int?` | FK → `Users` (set null on delete). Admin who reviewed. |
| `ReviewedAt` | `DateTime?` | UTC timestamp of review decision. |
| `RejectionReason` | `string?` | Max 500 chars. Shown to the user when rejected. |
| `AdminBypassNote` | `string?` | Max 500 chars. Required when `Status = AdminBypassed`. |

**Indexes:** `UserId`, `Status`

---

## EF Migration

Migration name: `AddKycVerification`

Adds `PhoneNumber` and `KycStatus` to `Users`. Creates the `UserKycDocuments` table with all FK constraints and indexes.

---

## API Endpoints

**Controller:** `KycController`  
**File:** `server/src/Batanai.Api/Controllers/KycController.cs`  
Base route: `/api/kyc`  
All routes require `[Authorize]`.

| Method | Route | Who | Description |
|---|---|---|---|
| `POST` | `/submit` | Any authenticated user | Submit or resubmit KYC details |
| `GET` | `/status` | Any authenticated user | Get own KYC status and metadata |
| `GET` | `/pending` | Admin / SuperAdmin | List all `PendingReview` submissions (oldest first) |
| `POST` | `/{id}/review` | Admin / SuperAdmin | Approve or reject a specific submission |
| `POST` | `/bypass/{userId}` | Admin / SuperAdmin | Admin-bypass KYC for a user without a document review |

---

## Business Logic

**Service:** `KycService`  
**File:** `server/src/Batanai.Api/Services/KycService.cs`

### `SubmitKycAsync(userId, request)`

1. Validates `FullNameOnId` is present.
2. For non-`NoDocument` paths: requires both `IdNumber` and `DocumentFileId`.
3. Verifies any provided file IDs belong to the submitting user (prevents file-ID spoofing).
4. Creates a new `UserKycDocument` record with `Status = PendingReview`.
5. Sets `User.KycStatus = PendingReview`.
6. Pushes a `KycSubmitted` notification to all `Admin` / `SuperAdmin` users with deep link `/admin/kyc`.

### `GetKycStatusAsync(userId)`

Returns `KycStatusDto` — the user's current status, rejection reason (if any), submission/review timestamps, and a `canResubmit` flag (`true` when status is `Rejected`).

### `GetPendingKycAsync()`

Returns all `UserKycDocument` records with `Status = PendingReview`, ordered by `SubmittedAt` ascending (FIFO review queue).

### `ReviewKycAsync(kycId, adminId, approve, reason)`

1. Only acts on submissions in `PendingReview` state.
2. Rejection requires a non-empty `reason` (shown to the user).
3. Sets `doc.Status` and `User.KycStatus` to `Verified` or `Rejected`.
4. Records `ReviewedByUserId` and `ReviewedAt`.
5. Pushes `KycApproved` or `KycRejected` notification to the user with deep link `/profile`.
6. On approval, cancels any pending KYC reminder scheduled notifications for the user.

### `AdminBypassKycAsync(targetUserId, adminId, note)`

1. Requires a non-empty bypass note (stored as `AdminBypassNote`).
2. Creates a `UserKycDocument` with `IdType = NoDocument`, `Status = AdminBypassed`, `ReviewedByUserId = adminId`.
3. Sets `User.KycStatus = AdminBypassed`.
4. Pushes `KycBypassed` notification to the user with deep link `/profile`.
5. Cancels any pending KYC reminder notifications for the user.

---

## Enforcement Gates

KYC is enforced in three places inside `ExpenseCycleService` and `MukandoService`. All checks are for **Mukando cycles only**.

### 1. Add member to a Mukando cycle (`AddMemberAsync`)

Before adding a user, their `KycStatus` must be `Verified` or `AdminBypassed`. Returns an error naming the user if they fail.

### 2. Batch-add members to a Mukando cycle (`AddMembersBatchAsync`)

Same check, applied to every user in the batch. All failing user names are collected and returned in a single error message.

### 3. Start a Mukando cycle (`StartAsync`)

Before advancing a cycle from `Draft` to `Active`, all current members are checked. If any are not KYC-cleared, returns a blocking error listing every non-compliant member name.

### 4. Record Mukando payout (`RecordPayoutAsync`)

Before recording that a round recipient has been paid, their `KycStatus` is verified. Prevents payouts to users who somehow lost their verified status after the cycle started.

---

## Background KYC Reminders

**Service:** `BgTimerHostedService`  
**File:** `server/src/Batanai.Api/Services/BgTimerHostedService.cs`  
**Method:** `SendKycRemindersAsync`  
**Interval:** Runs every 5 minutes; sends to each user at most once per 24 hours.

**Algorithm:**

1. Find all Mukando cycles with `Status = Draft`.
2. Get the distinct set of member user IDs across those cycles.
3. Keep only users whose `KycStatus` is not `Verified` / `AdminBypassed`.
4. From those, exclude any user who already received a `KycReminder` notification in the last 24 hours (single batch query).
5. Send a `KycReminder` push notification to each remaining user with deep link `/profile`.

Push body:  
> "You are part of a Mukando cycle that requires identity verification. Complete your KYC in your profile to avoid being blocked when the cycle starts."

Each user's send is wrapped in a try/catch so a failed delivery does not block subsequent users. A summary log line is emitted at `Information` level showing total sent.

---

## Push Notification Types

| Type | ID | Trigger | Recipient |
|---|---|---|---|
| `KycSubmitted` | 42 | User submits KYC | All Admins / SuperAdmins |
| `KycApproved` | 43 | Admin approves submission | Submitting user |
| `KycRejected` | 44 | Admin rejects submission | Submitting user |
| `KycBypassed` | 45 | Admin bypasses KYC for user | Affected user |
| `KycReminder` | 46 | Background service (draft Mukando member) | Unverified member |

---

## File Privacy

**File:** `server/src/Batanai.Api/Controllers/FilesController.cs`

The file download endpoint checks whether the requested file is linked to any `UserKycDocument` (as `DocumentFileId` or `SelfieWithIdFileId`). If it is:

- The request must be authenticated.
- The caller must be either the file's original uploader or an `Admin` / `SuperAdmin`.
- Anonymous or unauthorized callers receive `403 Forbidden`.

Regular (non-KYC) files are unaffected.

---

## Angular Client

### Model — `user.model.ts`

```ts
export enum KycStatus {
  NotStarted    = 0,
  PendingReview = 1,
  Verified      = 2,
  Rejected      = 3,
  AdminBypassed = 4
}

// Added to User interface:
phoneNumber?: string;
kycStatus?:   KycStatus;
```

### Service — `kyc.service.ts`

**File:** `client/src/app/core/services/kyc.service.ts`

| Method | Description |
|---|---|
| `getStatus()` | `GET /api/kyc/status` → `KycStatusDto` |
| `submit(request)` | `POST /api/kyc/submit` → `KycDocumentDto` |
| `getPending()` | `GET /api/kyc/pending` → `KycDocumentDto[]` |
| `review(id, approve, reason?)` | `POST /api/kyc/{id}/review` |
| `bypass(userId, note)` | `POST /api/kyc/bypass/{userId}` |
| `uploadFile(file, folder?)` | `POST /api/files/upload?folder=kyc` → file ID (`number`) |

`uploadFile` extracts the numeric file ID from the returned URL using the pattern `/api/files/{id}`.

### Profile component (`profile.component.ts/html/css`)

Displays a KYC status card at the top of the profile page:

- **NotStarted / Rejected** — shows the KYC submission form (ID type, ID number, document upload, selfie upload). For `NoDocument`, the document/selfie fields are hidden and a plain-language instruction box is shown.
- **PendingReview** — shows a waiting state with submission timestamp.
- **Verified / AdminBypassed** — shows a green confirmation card; form is hidden.

On resubmit (after rejection), the full form is shown again and a new `UserKycDocument` is created on the backend.

### Cycle detail component (`cycle-detail.component.ts/html/css`)

- The **Start Cycle** button is disabled when `isMukando && !allMukandoMembersKycReady()`.
- The draft checklist shows a KYC readiness line: `{n} / {total} members KYC verified`.
- A `kyc-readiness-panel` lists every member who is not yet `Verified` / `AdminBypassed`, with a colour-coded status chip beside their name.

---

## Sequence: Normal User Flow

```
User                         API                         Admin
 |                            |                             |
 |-- POST /api/files/upload -->|                             |
 |<-- { url: "/api/files/42" } |                            |
 |                            |                             |
 |-- POST /api/kyc/submit ---->|                             |
 |   { idType, idNumber,      |                             |
 |     fullNameOnId,          |--- KycSubmitted push ------>|
 |     documentFileId: 42 }   |                             |
 |<-- 200 KycDocumentDto ------|                             |
 |                            |                             |
 |                            |<-- GET /api/kyc/pending ----|
 |                            |--- 200 [KycDocumentDto] --->|
 |                            |                             |
 |                            |<-- POST /api/kyc/7/review --|
 |                            |    { approve: true }        |
 |<-- KycApproved push --------|                             |
 |   deep link: /profile      |                             |
```

---

## Sequence: Admin Bypass Flow

```
Admin                         API                         User
 |                             |                            |
 |-- POST /api/kyc/bypass/99 ->|                            |
 |   { note: "Trusted member" }|                            |
 |<-- 204 No Content ----------|                            |
 |                             |--- KycBypassed push ------>|
 |                             |    deep link: /profile     |
```
