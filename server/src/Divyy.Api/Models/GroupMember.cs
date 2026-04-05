namespace Divvy.Api.Models;

public class GroupMember
{
    public int GroupId { get; set; }
    public int UserId { get; set; }

    public GroupRole GroupRole { get; set; } = GroupRole.GroupMember;

    public GroupInviteStatus Status { get; set; } = GroupInviteStatus.Pending;

    public int? InvitedByUserId { get; set; }

    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RespondedAt { get; set; }

    public DateTime? JoinRequestedAt { get; set; }

    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }
}
