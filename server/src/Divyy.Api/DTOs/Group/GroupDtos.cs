namespace Divvy.Api.DTOs.Group;

// ── Requests ──────────────────────────────────────────────────────────────────

public record CreateGroupRequest(
    string Name,
    string? Description);

public record UpdateGroupRequest(
    string Name,
    string? Description,
    bool IsActive);

public record InviteGroupMemberRequest(
    int UserId,
    string GroupRole);  // "GroupAdmin" | "GroupMember"

public record RespondToInviteRequest(
    bool Accept);

public record UpdateGroupMemberRoleRequest(
    string GroupRole);  // "GroupAdmin" | "GroupMember"

// ── Responses ─────────────────────────────────────────────────────────────────

public record GroupDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    bool CanManage);

public record GroupDetailDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    bool CanManage,
    IReadOnlyList<GroupMemberDto> Members);

public record GroupMemberDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    string GroupRole,
    string Status,
    DateTime InvitedAt,
    DateTime? RespondedAt);

public record GroupInviteDto(
    int GroupId,
    string GroupName,
    int InvitedByUserId,
    string InvitedByName,
    string GroupRole,
    DateTime InvitedAt,
    string Status);
