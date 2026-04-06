# Groups

## Overview

A **Group** is the top-level organisational unit in Batanai. It represents a community of users who participate in shared financial activities together. All expense cycles (Majana and Mukando) are created within the context of a group.

Groups are managed by one or more **Group Admins** who can invite members, approve join requests, create cycles, and manage group settings. Regular **Group Members** can view the group, participate in cycles, and leave at any time.

---

## Role Access

| Role | Access |
|---|---|
| SuperAdmin | Full CRUD on all groups and memberships |
| Admin | Full CRUD on all groups and memberships |
| Member | View own groups, accept/decline invites, join by code, leave group |
| Group Admin | Invite/remove members, change roles, create cycles, manage join requests, regenerate join code |
| Group Member | View group, participate in cycles, leave group |

---

## Backend

### Controller — `GroupController`

**File:** `server/src/Batanai.Api/Controllers/GroupController.cs`

Base route: `/api/groups` — all endpoints require `[Authorize]`

| Method | Route | Description |
|---|---|---|
| GET | `/` | List all groups (admins see all; members see their own) |
| GET | `/{id}` | Get group detail with members |
| POST | `/` | Create new group (creator becomes Group Admin) |
| PUT | `/{id}` | Update group name, description, or active status |
| DELETE | `/{id}` | Delete group (cascades to all memberships) |
| GET | `/{id}/members` | List all members (accepted + pending) |
| POST | `/{id}/invites` | Invite a user to the group |
| POST | `/{id}/invites/respond` | Accept or decline an invite |
| GET | `/my-invites` | Get current user's pending invites |
| DELETE | `/{id}/members/{targetUserId}` | Remove or uninvite a member |
| PUT | `/{id}/members/{targetUserId}/role` | Change a member's role |
| POST | `/{id}/leave` | Current user leaves the group |
| POST | `/join` | Request to join a group using a join code |
| POST | `/{id}/regenerate-code` | Generate a new join code (invalidates old one) |
| GET | `/{id}/join-requests` | Get pending join-by-code requests |
| POST | `/{id}/join-requests/{targetUserId}/respond` | Approve or decline a join request |

---

### Model — `Group`

**File:** `server/src/Batanai.Api/Models/Group.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `int` | PK |
| `Name` | `string` | Required, max 150 characters |
| `Description` | `string?` | Optional, max 500 characters |
| `IsActive` | `bool` | Defaults to `true` |
| `CreatedByUserId` | `int` | FK → User |
| `CreatedAt` | `DateTime` | UTC timestamp |
| `UpdatedAt` | `DateTime` | UTC timestamp |
| `JoinCode` | `string` | 8-character unique code, required |
| `JoinCodeGeneratedAt` | `DateTime` | When the current code was generated |

---

### Model — `GroupMember`

**File:** `server/src/Batanai.Api/Models/GroupMember.cs`

| Field | Type | Notes |
|---|---|---|
| `GroupId` | `int` | Composite PK — FK → Group (cascade delete) |
| `UserId` | `int` | Composite PK — FK → User (cascade delete) |
| `GroupRole` | `GroupRole` | `GroupAdmin = 1` or `GroupMember = 2` |
| `Status` | `GroupInviteStatus` | `Pending = 1`, `Accepted = 2`, `Declined = 3`, `JoinRequested = 4` |
| `InvitedByUserId` | `int?` | FK → User — who sent the invite |
| `InvitedAt` | `DateTime` | When the member was invited |
| `RespondedAt` | `DateTime?` | When the user accepted or declined |
| `JoinRequestedAt` | `DateTime?` | When the user submitted a join-by-code request |
| `ApprovedByUserId` | `int?` | FK → User — who approved the join request |
| `ApprovedAt` | `DateTime?` | When the join request was approved |

---

### Enums

**File:** `server/src/Batanai.Api/Models/BatanaiEnums.cs`

```csharp
public enum GroupRole
{
    GroupAdmin = 1,
    GroupMember = 2
}

public enum GroupInviteStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    JoinRequested = 4
}
```

---

### Service — `GroupService`

**File:** `server/src/Batanai.Api/Services/GroupService.cs`
**Interface:** `server/src/Batanai.Api/Services/IGroupService.cs`

#### Query Methods

| Method | Returns | Description |
|---|---|---|
| `GetAllAsync(userId, userRole)` | `List<GroupDto>` | SuperAdmin/Admin see all; members see groups they belong to |
| `GetByIdAsync(groupId, userId, userRole)` | `GroupDetailDto?` | Group detail with full member list |
| `GetMembersAsync(groupId, userId, userRole)` | `List<GroupMemberDto>` | All members (accepted + pending) |
| `GetPendingInvitesForUserAsync(userId)` | `List<GroupInviteDto>` | Current user's pending invitations |
| `GetPendingJoinRequestsAsync(groupId, userId, userRole)` | `List<JoinRequestDto>` | Pending join-by-code requests for admins |

#### Mutation Methods

| Method | Returns | Description |
|---|---|---|
| `CreateAsync(request, creatorId)` | `(GroupDto?, string?)` | Creates group; creator auto-becomes Group Admin with `Accepted` status |
| `UpdateAsync(groupId, request, userId, userRole)` | `(GroupDto?, string?)` | Update name, description, active status |
| `DeleteAsync(groupId, userId, userRole)` | `string?` | Delete group (cascade deletes all memberships) |
| `InviteMemberAsync(groupId, request, inviterId, inviterRole)` | `(GroupMemberDto?, string?)` | Creates `GroupMember` with `Pending` status; sends notification |
| `RespondToInviteAsync(groupId, userId, request)` | `string?` | Accept or decline; if accepted, notifies existing members |
| `RemoveMemberAsync(groupId, targetUserId, userId, userRole)` | `string?` | Remove member; cannot remove last Group Admin |
| `UpdateMemberRoleAsync(groupId, targetUserId, request, userId, userRole)` | `string?` | Change role (Admin ↔ Member); cannot demote last Group Admin |
| `LeaveGroupAsync(groupId, userId)` | `string?` | User leaves; cannot leave if last Group Admin |
| `RequestJoinByCodeAsync(joinCode, userId)` | `(JoinByCodeResponse?, string?)` | Creates member with `JoinRequested` status; notifies all Group Admins |
| `RespondToJoinRequestAsync(groupId, requestingUserId, approve, respondingUserId, userRole)` | `string?` | Approve → `Accepted` + notify; decline → `Declined` + notify |
| `RegenerateJoinCodeAsync(groupId, userId, userRole)` | `(string?, string?, string?)` | Generates new 8-char code; old code becomes invalid |
| `IsGroupAdminOfGroupAsync(groupId, userId)` | `bool` | Check if user is admin of specific group |

#### Join Code Generation

Join codes are 8 characters drawn from `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (excludes ambiguous characters: `0`, `O`, `1`, `I`, `L`). Generated using `RandomNumberGenerator.Fill()` for cryptographic security, with up to 10 retry attempts to ensure uniqueness.

#### Last-Admin Protection

The service prevents the group from losing all administrators:

- Cannot **remove** the last accepted Group Admin
- Cannot **demote** the last accepted Group Admin to Member
- Cannot **leave** the group if you are the last Group Admin

---

### DTOs

**File:** `server/src/Batanai.Api/DTOs/Group/GroupDtos.cs`

#### Request DTOs

| DTO | Fields |
|---|---|
| `CreateGroupRequest` | `Name`, `Description?` |
| `UpdateGroupRequest` | `Name`, `Description?`, `IsActive` |
| `InviteGroupMemberRequest` | `UserId`, `GroupRole` |
| `RespondToInviteRequest` | `Accept` (bool) |
| `UpdateGroupMemberRoleRequest` | `GroupRole` |
| `JoinByCodeRequest` | `JoinCode` |
| `RespondToJoinRequestRequest` | `Approve` (bool) |

#### Response DTOs

| DTO | Fields |
|---|---|
| `GroupDto` | `Id`, `Name`, `Description?`, `IsActive`, `MemberCount`, `CreatedAt`, `CanManage`, `JoinCode?`, `JoinUrl?` |
| `GroupDetailDto` | Extends `GroupDto` + `Members: IReadOnlyList<GroupMemberDto>` |
| `GroupMemberDto` | `UserId`, `FirstName`, `LastName`, `Email`, `GroupRole`, `Status`, `InvitedAt`, `RespondedAt?` |
| `GroupInviteDto` | `GroupId`, `GroupName`, `InvitedByUserId`, `InvitedByName`, `GroupRole`, `InvitedAt`, `Status` |
| `JoinByCodeResponse` | `GroupId`, `GroupName`, `Message` |
| `JoinRequestDto` | `UserId`, `FirstName`, `LastName`, `Email`, `RequestedAt` |

---

### Database Configuration

**File:** `server/src/Batanai.Api/Data/ApplicationDbContext.cs`

```csharp
public DbSet<Group> Groups { get; set; } = null!;
public DbSet<GroupMember> GroupMembers { get; set; } = null!;
```

**Group entity** — Table `"Groups"`:
- `JoinCode`: Required, max 8 chars, unique index
- `Name`: Required, max 150 chars
- `CreatedByUserId`: FK → User with Restrict delete

**GroupMember entity** — Table `"GroupMembers"`:
- Composite key: `(GroupId, UserId)`
- `GroupId` → Group with Cascade delete
- `UserId` → User with Cascade delete
- `InvitedByUserId` → User with Restrict delete
- `ApprovedByUserId` → User with Restrict delete

---

## Frontend

### Module

**File:** `client/src/app/features/groups/groups.module.ts`

Declares 5 components:
1. `GroupListComponent`
2. `GroupDetailComponent`
3. `CreateGroupDialogComponent`
4. `GroupMembersDialogComponent`
5. `JoinGroupDialogComponent`

**Route:** `/groups` (lazy-loaded)

---

### Models

**File:** `client/src/app/shared/models/group.model.ts`

Mirrors server DTOs as TypeScript interfaces:

```typescript
interface GroupDto {
  id: number; name: string; description?: string; isActive: boolean;
  memberCount: number; createdAt: string; canManage: boolean;
  joinCode?: string; joinUrl?: string;
}

interface GroupDetailDto extends GroupDto {
  members: GroupMemberDto[];
}

interface GroupMemberDto {
  userId: number; firstName: string; lastName: string; email: string;
  groupRole: string; status: string; invitedAt: string; respondedAt?: string;
}

interface GroupInviteDto {
  groupId: number; groupName: string; invitedByUserId: number;
  invitedByName: string; groupRole: string; invitedAt: string; status: string;
}

interface JoinByCodeResponse { groupId: number; groupName: string; message: string; }
interface JoinRequestDto { userId: number; firstName: string; lastName: string; email: string; requestedAt: string; }
```

---

### Service

**File:** `client/src/app/core/services/group.service.ts`

| Method | HTTP | Endpoint |
|---|---|---|
| `getGroups()` | GET | `/api/groups` |
| `getGroup(id)` | GET | `/api/groups/{id}` |
| `createGroup(req)` | POST | `/api/groups` |
| `updateGroup(id, req)` | PUT | `/api/groups/{id}` |
| `deleteGroup(id)` | DELETE | `/api/groups/{id}` |
| `getGroupMembers(groupId)` | GET | `/api/groups/{groupId}/members` |
| `inviteMember(groupId, req)` | POST | `/api/groups/{groupId}/invites` |
| `removeMember(groupId, userId)` | DELETE | `/api/groups/{groupId}/members/{userId}` |
| `updateMemberRole(groupId, userId, req)` | PUT | `/api/groups/{groupId}/members/{userId}/role` |
| `getMyInvites()` | GET | `/api/groups/my-invites` |
| `respondToInvite(groupId, req)` | POST | `/api/groups/{groupId}/invites/respond` |
| `leaveGroup(groupId)` | POST | `/api/groups/{groupId}/leave` |
| `joinByCode(req)` | POST | `/api/groups/join` |
| `regenerateJoinCode(groupId)` | POST | `/api/groups/{groupId}/regenerate-code` |
| `getJoinRequests(groupId)` | GET | `/api/groups/{groupId}/join-requests` |
| `respondToJoinRequest(groupId, userId, req)` | POST | `/api/groups/{groupId}/join-requests/{userId}/respond` |

---

### Components

#### GroupListComponent

**File:** `client/src/app/features/groups/group-list/group-list.component.ts`
**Route:** `/groups`

- Displays groups in a table with columns: Name, Member Count, Status, Created Date, Actions
- **Auto-join**: If `?join=CODE` is in the URL (e.g. from a QR scan), automatically opens the join dialog pre-filled
- Action buttons: Create Group, Join Group, Manage Members, Edit, Delete, View Details
- Empty state: "No groups yet. Create one to get started."

#### GroupDetailComponent

**File:** `client/src/app/features/groups/group-detail/group-detail.component.ts`
**Route:** `/groups/{id}`

- Group info card with name, description, and status badge
- **Join Code Section** (Group Admins only):
  - Displays the 8-character join code
  - QR code rendered via `angularx-qrcode`
  - Copy to clipboard button
  - Share button (uses `navigator.share` API where available)
  - Regenerate button with confirmation warning
  - Hint text explaining admin approval is required
- **Cycles Section**: Table of cycles within the group with navigation to cycle detail
- **Actions**: Manage Members, Edit Group, Leave Group (members), New Cycle (admins)

#### CreateGroupDialogComponent

**File:** `client/src/app/features/groups/create-group-dialog/create-group-dialog.component.ts`

Modal dialog for creating or editing a group:
- **Name** field: Required, max 150 characters
- **Description** field: Optional, max 500 characters
- **IsActive** toggle (edit mode only)
- Pre-populates fields when editing an existing group

#### GroupMembersDialogComponent

**File:** `client/src/app/features/groups/group-members-dialog/group-members-dialog.component.ts`

Modal dialog with different views based on role:

**Admin view:**
- **Invite Section**: User search dropdown + role selector (Member / Admin) + Send Invite button
- **Accepted Members Table**: Name, Email, Role (inline editable), Status badge, Remove button
- **Pending Invites Table**: Name, Email, Role, Status badge, Cancel button
- **Join Requests Table**: Name, Email, Requested date, Approve / Decline buttons

**Member view (read-only):**
- Accepted members table without edit/remove actions
- Invite and join request sections hidden

#### JoinGroupDialogComponent

**File:** `client/src/app/features/groups/join-group-dialog/join-group-dialog.component.ts`

Modal dialog for joining a group by code:
- Text input for 8-character code (uppercase enforced)
- Auto-populates if `?join=CODE` was in the URL
- Shows success or error messages
- Closes automatically on success

---

## Notifications

| Type | ID | Trigger | Recipients | Delivery |
|---|---|---|---|---|
| `GroupInviteReceived` | 6 | Admin invites user | Invited user | In-app |
| `MemberJoinedGroup` | 16 | User accepts invite or join request approved | Existing accepted members | Push |
| `MemberLeftGroup` | 15 | User leaves or is removed | Removed user + remaining members | Push |
| `JoinRequestReceived` | 33 | User submits join code | All Group Admins | Push |
| `JoinRequestApproved` | 34 | Admin approves join request | Requesting user | Push |
| `JoinRequestDeclined` | 35 | Admin declines join request | Requesting user | Push |

---

## Workflows

### Create Group

1. User clicks **New Group** on the groups list
2. Modal opens with name and description fields
3. User submits → `POST /api/groups`
4. Server creates `Group` with a generated join code
5. Creator is automatically added as `GroupAdmin` with `Accepted` status
6. Modal closes, groups list reloads

### Invite Member

1. Group Admin opens **Manage Members** dialog
2. Searches for user by name or email, selects role
3. Clicks **Send Invite** → `POST /api/groups/{id}/invites`
4. Server creates `GroupMember` with `Status = Pending`
5. Invited user receives `GroupInviteReceived` in-app notification
6. Members table updates with new pending entry

### Accept / Decline Invite

1. User receives `GroupInviteReceived` notification
2. Navigates to pending invites (via notification or `/groups/my-invites`)
3. Clicks Accept or Decline → `POST /api/groups/{id}/invites/respond`
4. If accepted: status → `Accepted`; existing members receive `MemberJoinedGroup` push notification
5. If declined: status → `Declined`

### Join by Code

1. Group Admin shares the 8-character join code or QR link
2. User enters code in **Join Group** dialog or scans QR → `POST /api/groups/join`
3. Server creates `GroupMember` with `Status = JoinRequested`
4. All Group Admins receive `JoinRequestReceived` push notification
5. Admin approves or declines → `POST /api/groups/{id}/join-requests/{userId}/respond`
6. If approved: status → `Accepted`; user receives `JoinRequestApproved`; existing members receive `MemberJoinedGroup`
7. If declined: status → `Declined`; user receives `JoinRequestDeclined`

### Leave Group

1. Member clicks **Leave Group** on group detail
2. Confirmation dialog appears
3. User confirms → `POST /api/groups/{id}/leave`
4. Server removes `GroupMember` (blocked if last Group Admin)
5. Remaining members receive `MemberLeftGroup` push notification
6. User is redirected to `/groups`

### Regenerate Join Code

1. Group Admin clicks **Regenerate** on the join code section
2. Confirmation warning: old code becomes invalid immediately
3. User confirms → `POST /api/groups/{id}/regenerate-code`
4. Server generates new 8-character code
5. QR code and displayed code update in the UI
