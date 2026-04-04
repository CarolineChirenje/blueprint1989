using Microsoft.EntityFrameworkCore;
using Divvy.Api.Data;
using Divvy.Api.DTOs.Group;
using Divvy.Api.Models;

namespace Divvy.Api.Services;

public class GroupService : IGroupService
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService  _notifications;
    private readonly IPushNotificationSender _push;

    public GroupService(ApplicationDbContext context, NotificationService notifications, IPushNotificationSender push)
    {
        _context       = context;
        _notifications = notifications;
        _push          = push;
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    public async Task<List<GroupDto>> GetAllAsync(int userId, Role userRole)
    {
        var isAdmin = userRole == Role.SuperAdmin || userRole == Role.Admin;

        IQueryable<Group> query = _context.Groups.Where(g => g.IsActive);

        if (!isAdmin)
        {
            // GroupAdmins see groups they created; GroupMembers see groups where they were invited and accepted.
            var createdGroupIds = await _context.Groups
                .Where(g => g.CreatedByUserId == userId)
                .Select(g => g.Id)
                .ToListAsync();

            var memberGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == userId && gm.Status == GroupInviteStatus.Accepted)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            var visibleIds = createdGroupIds.Union(memberGroupIds).ToList();
            query = query.Where(g => visibleIds.Contains(g.Id));
        }

        var groups = await query.OrderBy(g => g.Name).ToListAsync();

        var result = new List<GroupDto>();
        foreach (var g in groups)
        {
            var memberCount = await _context.GroupMembers
                .CountAsync(gm => gm.GroupId == g.Id && gm.Status == GroupInviteStatus.Accepted);

            var canManage = isAdmin || await IsGroupAdminAsync(g.Id, userId);
            result.Add(new GroupDto(g.Id, g.Name, g.Description, g.IsActive, memberCount, g.CreatedAt, canManage));
        }

        return result;
    }

    public async Task<GroupDetailDto?> GetByIdAsync(int groupId, int userId, Role userRole)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return null;

        if (!await CanAccessGroupAsync(groupId, userId, userRole))
            return null;

        var members = await GetMembersAsync(groupId, userId, userRole);
        var memberCount = members.Count(m => m.Status == GroupInviteStatus.Accepted.ToString());
        var canManage = await CanManageGroupAsync(groupId, userId, userRole);

        return new GroupDetailDto(
            group.Id, group.Name, group.Description, group.IsActive,
            memberCount, group.CreatedAt, canManage, members);
    }

    public async Task<List<GroupMemberDto>> GetMembersAsync(int groupId, int userId, Role userRole)
    {
        if (!await CanAccessGroupAsync(groupId, userId, userRole))
            return new List<GroupMemberDto>();

        var rows = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .Join(_context.Users,
                  gm => gm.UserId,
                  u  => u.Id,
                  (gm, u) => new { gm, u })
            .ToListAsync();

        return rows.Select(r => new GroupMemberDto(
            r.u.Id,
            r.u.FirstName,
            r.u.LastName,
            r.u.Email,
            r.gm.GroupRole.ToString(),
            r.gm.Status.ToString(),
            r.gm.InvitedAt,
            r.gm.RespondedAt))
        .OrderBy(m => m.Status)
        .ThenBy(m => m.LastName)
        .ToList();
    }

    public async Task<List<GroupInviteDto>> GetPendingInvitesForUserAsync(int userId)
    {
        var rows = await _context.GroupMembers
            .Where(gm => gm.UserId == userId && gm.Status == GroupInviteStatus.Pending)
            .Join(_context.Groups,
                  gm => gm.GroupId,
                  g  => g.Id,
                  (gm, g) => new { gm, g })
            .Join(_context.Users,
                  x  => x.gm.InvitedByUserId,
                  u  => u.Id,
                  (x, u) => new { x.gm, x.g, inviter = u })
            .ToListAsync();

        return rows.Select(r => new GroupInviteDto(
            r.g.Id,
            r.g.Name,
            r.inviter.Id,
            $"{r.inviter.FirstName} {r.inviter.LastName}".Trim(),
            r.gm.GroupRole.ToString(),
            r.gm.InvitedAt,
            r.gm.Status.ToString()))
        .ToList();
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<(GroupDto? dto, string? error)> CreateAsync(CreateGroupRequest request, int creatorId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Name is required.");

        var group = new Group
        {
            Name             = request.Name.Trim(),
            Description      = request.Description?.Trim(),
            IsActive         = true,
            CreatedByUserId  = creatorId,
            CreatedAt        = DateTime.UtcNow,
            UpdatedAt        = DateTime.UtcNow
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        // Creator is automatically an accepted GroupAdmin — no invite required.
        _context.GroupMembers.Add(new GroupMember
        {
            GroupId         = group.Id,
            UserId          = creatorId,
            GroupRole       = GroupRole.GroupAdmin,
            Status          = GroupInviteStatus.Accepted,
            InvitedByUserId = creatorId,
            InvitedAt       = DateTime.UtcNow,
            RespondedAt     = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return (new GroupDto(group.Id, group.Name, group.Description, group.IsActive, 1, group.CreatedAt, true), null);
    }

    public async Task<(GroupDto? dto, string? error)> UpdateAsync(int groupId, UpdateGroupRequest request, int userId, Role userRole)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return (null, "Group not found.");

        if (!await CanManageGroupAsync(groupId, userId, userRole))
            return (null, "Forbidden.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Name is required.");

        group.Name        = request.Name.Trim();
        group.Description = request.Description?.Trim();
        group.IsActive    = request.IsActive;
        group.UpdatedAt   = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var memberCount = await _context.GroupMembers
            .CountAsync(gm => gm.GroupId == groupId && gm.Status == GroupInviteStatus.Accepted);

        return (new GroupDto(group.Id, group.Name, group.Description, group.IsActive, memberCount, group.CreatedAt, true), null);
    }

    public async Task<string?> DeleteAsync(int groupId, int userId, Role userRole)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return "Group not found.";

        _context.Groups.Remove(group);
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<(GroupMemberDto? dto, string? error)> InviteMemberAsync(
        int groupId, InviteGroupMemberRequest request, int inviterId, Role inviterRole)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return (null, "Group not found.");

        if (!await CanManageGroupAsync(groupId, inviterId, inviterRole))
            return (null, "Forbidden.");

        var targetUser = await _context.Users.FindAsync(request.UserId);
        if (targetUser == null) return (null, "User not found.");

        if (!targetUser.IsActive) return (null, "User account is inactive.");

        var existing = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == request.UserId);

        if (existing != null)
        {
            if (existing.Status == GroupInviteStatus.Accepted)
                return (null, "User is already a member of this group.");
            if (existing.Status == GroupInviteStatus.Pending)
                return (null, "User already has a pending invite to this group.");
            // Declined — re-invite by resetting
            existing.Status          = GroupInviteStatus.Pending;
            existing.GroupRole       = ParseGroupRole(request.GroupRole);
            existing.InvitedByUserId = inviterId;
            existing.InvitedAt       = DateTime.UtcNow;
            existing.RespondedAt     = null;
        }
        else
        {
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId         = groupId,
                UserId          = request.UserId,
                GroupRole       = ParseGroupRole(request.GroupRole),
                Status          = GroupInviteStatus.Pending,
                InvitedByUserId = inviterId,
                InvitedAt       = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Send in-app notification to the invited user
        var inviter = await _context.Users.FindAsync(inviterId);
        var inviterName = inviter != null ? $"{inviter.FirstName} {inviter.LastName}".Trim() : "An admin";

        await _notifications.CreateAsync(
            userId: request.UserId,
            message: $"{inviterName} has invited you to join the group \"{group.Name}\".",
            type: NotificationType.GroupInviteReceived,
            deepLinkUrl: "/notifications",
            relatedEntityId: groupId);

        var member = await _context.GroupMembers
            .FirstAsync(gm => gm.GroupId == groupId && gm.UserId == request.UserId);

        return (new GroupMemberDto(
            targetUser.Id,
            targetUser.FirstName,
            targetUser.LastName,
            targetUser.Email,
            member.GroupRole.ToString(),
            member.Status.ToString(),
            member.InvitedAt,
            member.RespondedAt), null);
    }

    public async Task<string?> RespondToInviteAsync(int groupId, int userId, RespondToInviteRequest request)
    {
        var invite = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

        if (invite == null) return "Invite not found.";
        if (invite.Status != GroupInviteStatus.Pending) return "Invite is no longer pending.";

        invite.Status      = request.Accept ? GroupInviteStatus.Accepted : GroupInviteStatus.Declined;
        invite.RespondedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        if (request.Accept)
        {
            var group       = await _context.Groups.FindAsync(groupId);
            var joiningUser = await _context.Users.FindAsync(userId);
            var joinerName  = joiningUser != null ? $"{joiningUser.FirstName} {joiningUser.LastName}".Trim() : "A member";

            // Notify all other accepted members that this user has joined
            var otherMemberIds = await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId
                          && gm.Status == GroupInviteStatus.Accepted
                          && gm.UserId != userId)
                .Select(gm => gm.UserId)
                .ToListAsync();

            if (group != null && otherMemberIds.Count > 0)
            {
                await _push.SendToUsersAsync(
                    userIds: otherMemberIds,
                    type: NotificationType.MemberJoinedGroup,
                    title: group.Name,
                    body: $"{joinerName} has joined the group.",
                    deepLinkUrl: $"/groups/{groupId}",
                    relatedEntityId: groupId);
            }
        }

        return null;
    }

    public async Task<string?> RemoveMemberAsync(int groupId, int targetUserId, int userId, Role userRole)
    {
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == targetUserId);

        if (member == null) return "Member not found in this group.";

        if (!await CanManageGroupAsync(groupId, userId, userRole))
            return "Forbidden.";

        // Prevent removing the last accepted GroupAdmin
        if (member.GroupRole == GroupRole.GroupAdmin && member.Status == GroupInviteStatus.Accepted)
        {
            var adminCount = await _context.GroupMembers
                .CountAsync(gm => gm.GroupId == groupId
                               && gm.GroupRole == GroupRole.GroupAdmin
                               && gm.Status == GroupInviteStatus.Accepted);
            if (adminCount <= 1)
                return "Cannot remove the last group admin.";
        }

        // Load group and removed user details before removal for notification
        var group       = await _context.Groups.FindAsync(groupId);
        var removedUser = await _context.Users.FindAsync(targetUserId);
        var removedName = removedUser != null ? $"{removedUser.FirstName} {removedUser.LastName}".Trim() : "A member";
        var wasAccepted = member.Status == GroupInviteStatus.Accepted;

        _context.GroupMembers.Remove(member);
        await _context.SaveChangesAsync();

        // Only notify if the removed user was an active member (not a pending invite)
        if (wasAccepted && group != null)
        {
            var remainingMemberIds = await _context.GroupMembers
                .Where(gm => gm.GroupId == groupId && gm.Status == GroupInviteStatus.Accepted)
                .Select(gm => gm.UserId)
                .ToListAsync();

            // Notify the removed member personally
            await _push.SendToUserAsync(
                userId: targetUserId,
                type: NotificationType.MemberLeftGroup,
                title: group.Name,
                body: "You have been removed from the group.",
                deepLinkUrl: "/groups",
                relatedEntityId: groupId);

            // Notify all remaining members sequentially (shared DbContext — no Task.WhenAll)
            if (remainingMemberIds.Count > 0)
            {
                await _push.SendToUsersAsync(
                    userIds: remainingMemberIds,
                    type: NotificationType.MemberLeftGroup,
                    title: group.Name,
                    body: $"{removedName} was removed from the group.",
                    deepLinkUrl: $"/groups/{groupId}",
                    relatedEntityId: groupId);
            }
        }

        return null;
    }

    public async Task<string?> UpdateMemberRoleAsync(int groupId, int targetUserId,
        UpdateGroupMemberRoleRequest request, int userId, Role userRole)
    {
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == targetUserId);

        if (member == null) return "Member not found in this group.";
        if (member.Status != GroupInviteStatus.Accepted) return "Cannot change role of a non-accepted member.";

        if (!await CanManageGroupAsync(groupId, userId, userRole))
            return "Forbidden.";

        var newRole = ParseGroupRole(request.GroupRole);

        // Only Admin/SuperAdmin can promote to GroupAdmin
        if (newRole == GroupRole.GroupAdmin && userRole == Role.Member)
            return "Forbidden.";

        // Prevent demoting the last GroupAdmin
        if (member.GroupRole == GroupRole.GroupAdmin && newRole == GroupRole.GroupMember)
        {
            var adminCount = await _context.GroupMembers
                .CountAsync(gm => gm.GroupId == groupId
                               && gm.GroupRole == GroupRole.GroupAdmin
                               && gm.Status == GroupInviteStatus.Accepted);
            if (adminCount <= 1)
                return "Cannot demote the last group admin.";
        }

        member.GroupRole = newRole;
        await _context.SaveChangesAsync();
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public Task<bool> IsGroupAdminOfGroupAsync(int groupId, int userId)
        => IsGroupAdminAsync(groupId, userId);

    private async Task<bool> IsGroupAdminAsync(int groupId, int userId)
        => await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId
                         && gm.UserId == userId
                         && gm.GroupRole == GroupRole.GroupAdmin
                         && gm.Status == GroupInviteStatus.Accepted);

    private async Task<bool> CanManageGroupAsync(int groupId, int userId, Role userRole)
    {
        if (userRole == Role.SuperAdmin || userRole == Role.Admin) return true;
        return await IsGroupAdminAsync(groupId, userId);
    }

    private async Task<bool> CanAccessGroupAsync(int groupId, int userId, Role userRole)
    {
        if (userRole == Role.SuperAdmin || userRole == Role.Admin) return true;
        return await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId
                         && gm.UserId == userId
                         && gm.Status == GroupInviteStatus.Accepted);
    }

    public async Task<string?> LeaveGroupAsync(int groupId, int userId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return "Group not found.";

        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId
                                    && gm.UserId == userId
                                    && gm.Status == GroupInviteStatus.Accepted);

        if (membership == null) return "You are not a member of this group.";

        // Prevent the last GroupAdmin from leaving
        if (membership.GroupRole == GroupRole.GroupAdmin)
        {
            var adminCount = await _context.GroupMembers
                .CountAsync(gm => gm.GroupId == groupId
                               && gm.GroupRole == GroupRole.GroupAdmin
                               && gm.Status == GroupInviteStatus.Accepted);
            if (adminCount <= 1)
                return "Cannot leave — you are the only admin. Assign another admin first.";
        }

        var leavingUser = await _context.Users.FindAsync(userId);
        var leaverName  = leavingUser != null ? $"{leavingUser.FirstName} {leavingUser.LastName}".Trim() : "A member";

        _context.GroupMembers.Remove(membership);
        await _context.SaveChangesAsync();

        // Notify all remaining accepted members
        var remainingMemberIds = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.Status == GroupInviteStatus.Accepted)
            .Select(gm => gm.UserId)
            .ToListAsync();

        if (remainingMemberIds.Count > 0)
        {
            await _push.SendToUsersAsync(
                userIds: remainingMemberIds,
                type: NotificationType.MemberLeftGroup,
                title: group.Name,
                body: $"{leaverName} has left the group.",
                deepLinkUrl: $"/groups/{groupId}",
                relatedEntityId: groupId);
        }

        return null;
    }

    private static GroupRole ParseGroupRole(string value)
        => value?.Trim().ToLower() == "groupadmin" ? GroupRole.GroupAdmin : GroupRole.GroupMember;
}
