using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Group;
using Batanai.Api.Models;
using Microsoft.Extensions.Logging;

namespace Batanai.Api.Services;

public class GroupService : IGroupService
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService  _notifications;
    private readonly IPushNotificationSender _push;
    private readonly ILogger<GroupService> _logger;
    private readonly AppConfigService _appConfig;

    public GroupService(ApplicationDbContext context, NotificationService notifications, IPushNotificationSender push, ILogger<GroupService> logger, AppConfigService appConfig)
    {
        _context       = context;
        _notifications = notifications;
        _push          = push;
        _logger        = logger;
        _appConfig     = appConfig;
    }

    private async Task<string?> BuildJoinUrlAsync(string? joinCode)
    {
        if (string.IsNullOrEmpty(joinCode)) return null;
        var domain = await _appConfig.GetStringAsync(AppConfigKeys.AppDomain, "");
        if (string.IsNullOrEmpty(domain)) return null;
        return $"{domain.TrimEnd('/')}/groups?join={joinCode}";
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
            var code = canManage ? g.JoinCode : null;
            var joinUrl = canManage ? await BuildJoinUrlAsync(g.JoinCode) : null;
            result.Add(new GroupDto(g.Id, g.Name, g.Description, g.IsActive, memberCount, g.CreatedAt, canManage,
                code, joinUrl));
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

        var code = canManage ? group.JoinCode : null;
        var joinUrl = canManage ? await BuildJoinUrlAsync(group.JoinCode) : null;

        return new GroupDetailDto(
            group.Id, group.Name, group.Description, group.IsActive,
            memberCount, group.CreatedAt, canManage, members,
            code, joinUrl);
    }

    public async Task<List<GroupMemberDto>> GetMembersAsync(int groupId, int userId, Role userRole)
    {
        if (!await CanAccessGroupAsync(groupId, userId, userRole))
            return new List<GroupMemberDto>();

        var rows = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.Status != GroupInviteStatus.JoinRequested)
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
            .GroupJoin(_context.Users,
                  x  => x.gm.InvitedByUserId,
                  u  => u.Id,
                  (x, users) => new { x.gm, x.g, users })
            .SelectMany(x => x.users.DefaultIfEmpty(),
                  (x, u) => new { x.gm, x.g, inviter = u })
            .ToListAsync();

        return rows.Select(r => new GroupInviteDto(
            r.g.Id,
            r.g.Name,
            r.inviter?.Id ?? 0,
            r.inviter != null ? $"{r.inviter.FirstName} {r.inviter.LastName}".Trim() : "Unknown",
            r.gm.GroupRole.ToString(),
            r.gm.InvitedAt,
            r.gm.Status.ToString()))
        .ToList();
    }

    public async Task<List<MyJoinRequestDto>> GetMyJoinRequestsAsync(int userId)
    {
        var rows = await _context.GroupMembers
            .Where(gm => gm.UserId == userId && gm.Status == GroupInviteStatus.JoinRequested)
            .Join(_context.Groups.Where(g => g.IsActive),
                  gm => gm.GroupId,
                  g  => g.Id,
                  (gm, g) => new { gm, g })
            .ToListAsync();

        return rows.Select(r => new MyJoinRequestDto(
            r.g.Id,
            r.g.Name,
            r.g.Description,
            r.gm.JoinRequestedAt ?? r.gm.InvitedAt))
        .OrderByDescending(r => r.RequestedAt)
        .ToList();
    }

    public async Task<string?> CancelJoinRequestAsync(int groupId, int userId)
    {
        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId
                                    && gm.UserId == userId
                                    && gm.Status == GroupInviteStatus.JoinRequested);

        if (member == null)
            return "Join request not found.";

        _context.GroupMembers.Remove(member);
        await _context.SaveChangesAsync();
        return null;
    }

    public async Task<List<AdminPendingJoinRequestDto>> GetAllPendingJoinRequestsForAdminAsync(int userId, Role userRole)
    {
        var isAdmin = userRole == Role.SuperAdmin || userRole == Role.Admin;

        IQueryable<GroupMember> query = _context.GroupMembers
            .Where(gm => gm.Status == GroupInviteStatus.JoinRequested);

        if (!isAdmin)
        {
            var managedGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == userId
                           && gm.GroupRole == GroupRole.GroupAdmin
                           && gm.Status == GroupInviteStatus.Accepted)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            query = query.Where(gm => managedGroupIds.Contains(gm.GroupId));
        }

        var rows = await query
            .Join(_context.Groups.Where(g => g.IsActive),
                  gm => gm.GroupId,
                  g  => g.Id,
                  (gm, g) => new { gm, g })
            .Join(_context.Users,
                  x  => x.gm.UserId,
                  u  => u.Id,
                  (x, u) => new { x.gm, x.g, u })
            .ToListAsync();

        return rows.Select(r => new AdminPendingJoinRequestDto(
            r.g.Id,
            r.g.Name,
            r.u.Id,
            r.u.FirstName,
            r.u.LastName,
            r.u.Email,
            r.gm.JoinRequestedAt ?? r.gm.InvitedAt))
        .OrderBy(r => r.RequestedAt)
        .ToList();
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public async Task<(GroupDto? dto, string? error)> CreateAsync(CreateGroupRequest request, int creatorId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return (null, "Name is required.");

        var group = new Group
        {
            Name                = request.Name.Trim(),
            Description         = request.Description?.Trim(),
            IsActive            = true,
            CreatedByUserId     = creatorId,
            CreatedAt           = DateTime.UtcNow,
            UpdatedAt           = DateTime.UtcNow,
            JoinCode            = await GenerateUniqueJoinCodeAsync(),
            JoinCodeGeneratedAt = DateTime.UtcNow
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

        return (new GroupDto(group.Id, group.Name, group.Description, group.IsActive, 1, group.CreatedAt, true, group.JoinCode), null);
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

        return (new GroupDto(group.Id, group.Name, group.Description, group.IsActive, memberCount, group.CreatedAt, true, group.JoinCode), null);
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
                    relatedEntityId: groupId,
                    excludeUserIds: new[] { userId });
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

        // Send notifications — wrapped so a delivery failure never blocks the removal response
        try
        {
            if (group != null)
            {
                // Always notify the removed/uninvited person
                await _push.SendToUserAsync(
                    userId: targetUserId,
                    type: NotificationType.MemberLeftGroup,
                    title: group.Name,
                    body: wasAccepted
                        ? "You have been removed from the group."
                        : "Your group invite has been cancelled.",
                    deepLinkUrl: "/groups",
                    relatedEntityId: groupId);

                // Only notify remaining accepted members if an active member was removed
                if (wasAccepted)
                {
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
                            body: $"{removedName} was removed from the group.",
                            deepLinkUrl: $"/groups/{groupId}",
                            relatedEntityId: groupId,
                            excludeUserIds: new[] { userId });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send removal notifications for user {TargetUserId} from group {GroupId}", targetUserId, groupId);
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

        // Notify all remaining accepted members — wrapped so a delivery failure never blocks the response
        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send leave notifications for user {UserId} from group {GroupId}", userId, groupId);
        }

        return null;
    }

    private static GroupRole ParseGroupRole(string value)
        => value?.Trim().ToLower() == "groupadmin" ? GroupRole.GroupAdmin : GroupRole.GroupMember;

    // ── Join-code helpers ─────────────────────────────────────────────────────

    private static readonly char[] JoinCodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789".ToCharArray(); // Excludes 0/O, 1/I/L

    private static string GenerateJoinCode()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return string.Create(8, bytes.ToArray(), (span, b) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = JoinCodeChars[b[i] % JoinCodeChars.Length];
        });
    }

    private async Task<string> GenerateUniqueJoinCodeAsync()
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var code = GenerateJoinCode();
            if (!await _context.Groups.AnyAsync(g => g.JoinCode == code))
                return code;
        }
        throw new InvalidOperationException("Failed to generate a unique join code after 10 attempts.");
    }

    // ── Join-by-code operations ───────────────────────────────────────────────

    public async Task<(JoinByCodeResponse? dto, string? error)> RequestJoinByCodeAsync(string joinCode, int userId)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
            return (null, "Join code is required.");

        var code = joinCode.Trim().ToUpperInvariant();
        var group = await _context.Groups.FirstOrDefaultAsync(g => g.JoinCode == code && g.IsActive);
        if (group == null)
            return (null, "Invalid join code.");

        var existing = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == group.Id && gm.UserId == userId);

        if (existing != null)
        {
            if (existing.Status == GroupInviteStatus.Accepted)
                return (null, "You are already a member of this group.");
            if (existing.Status == GroupInviteStatus.Pending)
                return (null, "You already have a pending invite to this group.");
            if (existing.Status == GroupInviteStatus.JoinRequested)
                return (null, "You already have a pending join request for this group.");
            // Declined — allow re-request
            existing.Status          = GroupInviteStatus.JoinRequested;
            existing.GroupRole       = GroupRole.GroupMember;
            existing.InvitedByUserId = null;
            existing.JoinRequestedAt = DateTime.UtcNow;
            existing.RespondedAt     = null;
            existing.ApprovedByUserId = null;
            existing.ApprovedAt      = null;
        }
        else
        {
            _context.GroupMembers.Add(new GroupMember
            {
                GroupId          = group.Id,
                UserId           = userId,
                GroupRole        = GroupRole.GroupMember,
                Status           = GroupInviteStatus.JoinRequested,
                InvitedByUserId  = null,
                InvitedAt        = DateTime.UtcNow,
                JoinRequestedAt  = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Notify all GroupAdmins of this group
        var requestingUser = await _context.Users.FindAsync(userId);
        var requesterName = requestingUser != null ? $"{requestingUser.FirstName} {requestingUser.LastName}".Trim() : "A user";

        var adminIds = await _context.GroupMembers
            .Where(gm => gm.GroupId == group.Id
                       && gm.GroupRole == GroupRole.GroupAdmin
                       && gm.Status == GroupInviteStatus.Accepted)
            .Select(gm => gm.UserId)
            .ToListAsync();

        if (adminIds.Count > 0)
        {
            await _push.SendToUsersAsync(
                userIds: adminIds,
                type: NotificationType.JoinRequestReceived,
                title: group.Name,
                body: $"{requesterName} has requested to join the group.",
                deepLinkUrl: "/notifications",
                relatedEntityId: group.Id);
        }

        return (new JoinByCodeResponse(group.Id, group.Name, "Your join request has been sent for approval."), null);
    }

    public async Task<string?> RespondToJoinRequestAsync(int groupId, int requestingUserId, bool approve, int respondingUserId, Role respondingUserRole)
    {
        if (!await CanManageGroupAsync(groupId, respondingUserId, respondingUserRole))
            return "Forbidden.";

        var member = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId
                                    && gm.UserId == requestingUserId
                                    && gm.Status == GroupInviteStatus.JoinRequested);

        if (member == null) return "Join request not found.";

        if (approve)
        {
            member.Status          = GroupInviteStatus.Accepted;
            member.ApprovedByUserId = respondingUserId;
            member.ApprovedAt      = DateTime.UtcNow;
            member.RespondedAt     = DateTime.UtcNow;
        }
        else
        {
            member.Status      = GroupInviteStatus.Declined;
            member.RespondedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Send notifications — wrapped so a delivery failure never blocks the response
        try
        {
            var group = await _context.Groups.FindAsync(groupId);
            if (group != null)
            {
                var notifType = approve ? NotificationType.JoinRequestApproved : NotificationType.JoinRequestDeclined;
                var body = approve
                    ? $"Your request to join \"{group.Name}\" has been approved."
                    : $"Your request to join \"{group.Name}\" has been declined.";

                _logger.LogInformation(
                    "Sending {NotifType} notification to user {UserId} for group {GroupId}",
                    notifType, requestingUserId, groupId);

                await _push.SendToUserAsync(
                    userId: requestingUserId,
                    type: notifType,
                    title: group.Name,
                    body: body,
                    deepLinkUrl: approve ? $"/groups/{groupId}" : "/groups",
                    relatedEntityId: groupId);

                // If approved, notify existing members that a new member joined
                if (approve)
                {
                    var joiningUser = await _context.Users.FindAsync(requestingUserId);
                    var joinerName = joiningUser != null ? $"{joiningUser.FirstName} {joiningUser.LastName}".Trim() : "A member";

                    var otherMemberIds = await _context.GroupMembers
                        .Where(gm => gm.GroupId == groupId
                                  && gm.Status == GroupInviteStatus.Accepted
                                  && gm.UserId != requestingUserId)
                        .Select(gm => gm.UserId)
                        .ToListAsync();

                    if (otherMemberIds.Count > 0)
                    {
                        await _push.SendToUsersAsync(
                            userIds: otherMemberIds,
                            type: NotificationType.MemberJoinedGroup,
                            title: group.Name,
                            body: $"{joinerName} has joined the group.",
                            deepLinkUrl: $"/groups/{groupId}",
                            relatedEntityId: groupId,
                            excludeUserIds: new[] { requestingUserId, respondingUserId });
        }

        return null;
    }

    public async Task<List<JoinRequestDto>> GetPendingJoinRequestsAsync(int groupId, int userId, Role userRole)
    {
        if (!await CanManageGroupAsync(groupId, userId, userRole))
            return new List<JoinRequestDto>();

        var rows = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId && gm.Status == GroupInviteStatus.JoinRequested)
            .Join(_context.Users,
                  gm => gm.UserId,
                  u  => u.Id,
                  (gm, u) => new { gm, u })
            .ToListAsync();

        return rows.Select(r => new JoinRequestDto(
            r.u.Id,
            r.u.FirstName,
            r.u.LastName,
            r.u.Email,
            r.gm.JoinRequestedAt ?? r.gm.InvitedAt))
        .OrderBy(r => r.RequestedAt)
        .ToList();
    }

    public async Task<(string? newCode, string? newUrl, string? error)> RegenerateJoinCodeAsync(int groupId, int userId, Role userRole)
    {
        if (!await CanManageGroupAsync(groupId, userId, userRole))
            return (null, null, "Forbidden.");

        var group = await _context.Groups.FindAsync(groupId);
        if (group == null) return (null, null, "Group not found.");

        group.JoinCode            = await GenerateUniqueJoinCodeAsync();
        group.JoinCodeGeneratedAt = DateTime.UtcNow;
        group.UpdatedAt           = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        var joinUrl = await BuildJoinUrlAsync(group.JoinCode);
        return (group.JoinCode, joinUrl, null);
    }
}
