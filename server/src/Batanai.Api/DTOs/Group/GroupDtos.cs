namespace Batanai.Api.DTOs.Group;

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

public record JoinByCodeRequest(
    string JoinCode);

public record RespondToJoinRequestRequest(
    bool Approve);

// ── Responses ─────────────────────────────────────────────────────────────────

public record GroupDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    bool CanManage,
    string? JoinCode = null,
    string? JoinUrl = null);

public record GroupDetailDto(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    bool CanManage,
    IReadOnlyList<GroupMemberDto> Members,
    string? JoinCode = null,
    string? JoinUrl = null);

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

public record JoinByCodeResponse(
    int GroupId,
    string GroupName,
    string Message);

public record JoinRequestDto(
    int UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime RequestedAt);
