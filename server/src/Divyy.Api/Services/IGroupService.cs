using Divvy.Api.DTOs.Group;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

public interface IGroupService
{
    Task<List<GroupDto>> GetAllAsync(int userId, Role userRole);
    Task<GroupDetailDto?> GetByIdAsync(int groupId, int userId, Role userRole);
    Task<(GroupDto? dto, string? error)> CreateAsync(CreateGroupRequest request, int creatorId);
    Task<(GroupDto? dto, string? error)> UpdateAsync(int groupId, UpdateGroupRequest request, int userId, Role userRole);
    Task<string?> DeleteAsync(int groupId, int userId, Role userRole);
    Task<List<GroupMemberDto>> GetMembersAsync(int groupId, int userId, Role userRole);
    Task<(GroupMemberDto? dto, string? error)> InviteMemberAsync(int groupId, InviteGroupMemberRequest request, int inviterId, Role inviterRole);
    Task<string?> RespondToInviteAsync(int groupId, int userId, RespondToInviteRequest request);
    Task<string?> RemoveMemberAsync(int groupId, int targetUserId, int userId, Role userRole);
    Task<string?> UpdateMemberRoleAsync(int groupId, int targetUserId, UpdateGroupMemberRoleRequest request, int userId, Role userRole);
    Task<List<GroupInviteDto>> GetPendingInvitesForUserAsync(int userId);
    Task<bool> IsGroupAdminOfGroupAsync(int groupId, int userId);
    Task<string?> LeaveGroupAsync(int groupId, int userId);
}
