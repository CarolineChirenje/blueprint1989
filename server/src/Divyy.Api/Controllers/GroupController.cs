using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Divvy.Api.DTOs.Group;
using Divvy.Api.Models;
using Divvy.Api.Services;

namespace Divvy.Api.Controllers;

[ApiController]
[Route("api/groups")]
[Authorize]
public class GroupController : ControllerBase
{
    private readonly IGroupService _groupService;

    public GroupController(IGroupService groupService)
    {
        _groupService = groupService;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    private Role GetCurrentUserRole()
    {
        var roleStr = User.FindFirst("role")?.Value ?? "";
        return roleStr == nameof(Role.SuperAdmin) ? Role.SuperAdmin
             : roleStr == nameof(Role.Admin)      ? Role.Admin
             : Role.Member;
    }

    /// <summary>Returns all groups. Admins see all; Members see only their accepted groups.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var groups = await _groupService.GetAllAsync(userId.Value, GetCurrentUserRole());
        return Ok(groups);
    }

    /// <summary>Returns full group detail including all members (accepted + pending).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var dto = await _groupService.GetByIdAsync(id, userId.Value, GetCurrentUserRole());
        if (dto == null) return NotFound(new { message = "Group not found or access denied." });
        return Ok(dto);
    }

    /// <summary>Creates a new group. Any authenticated user can create a group; they become its GroupAdmin automatically.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _groupService.CreateAsync(request, userId.Value);
        if (error != null) return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = dto!.Id }, dto);
    }

    /// <summary>Updates group name, description, or active status. Admin+ or GroupAdmin.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateGroupRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _groupService.UpdateAsync(id, request, userId.Value, GetCurrentUserRole());
        if (error == null) return Ok(dto);
        return error == "Group not found." ? NotFound(new { message = error })
             : error == "Forbidden."       ? Forbid()
             : BadRequest(new { message = error });
    }

    /// <summary>Deletes a group and all its memberships. Admin/SuperAdmin or GroupAdmin of the group.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _groupService.DeleteAsync(id, userId.Value, GetCurrentUserRole());
        if (error != null) return NotFound(new { message = error });
        return NoContent();
    }

    /// <summary>Returns all members of a group (accepted + pending). Admin+ or accepted group member.</summary>
    [HttpGet("{id:int}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var members = await _groupService.GetMembersAsync(id, userId.Value, GetCurrentUserRole());
        return Ok(members);
    }

    /// <summary>Invites a user to the group. Admin+ or GroupAdmin. Sends an in-app notification.</summary>
    [HttpPost("{id:int}/invites")]
    public async Task<IActionResult> InviteMember(int id, [FromBody] InviteGroupMemberRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _groupService.InviteMemberAsync(id, request, userId.Value, GetCurrentUserRole());
        if (error == null) return Ok(dto);
        return error == "Group not found." || error == "User not found." ? NotFound(new { message = error })
             : error == "Forbidden."                                     ? Forbid()
             : BadRequest(new { message = error });
    }

    /// <summary>Accept or decline a pending group invite. Called by the invited user only.</summary>
    [HttpPost("{id:int}/invites/respond")]
    public async Task<IActionResult> RespondToInvite(int id, [FromBody] RespondToInviteRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _groupService.RespondToInviteAsync(id, userId.Value, request);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Returns all pending group invites for the currently authenticated user.</summary>
    [HttpGet("my-invites")]
    public async Task<IActionResult> GetMyInvites()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var invites = await _groupService.GetPendingInvitesForUserAsync(userId.Value);
        return Ok(invites);
    }

    /// <summary>Removes an accepted or pending member from the group. Admin+ or GroupAdmin.</summary>
    [HttpDelete("{id:int}/members/{targetUserId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int targetUserId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _groupService.RemoveMemberAsync(id, targetUserId, userId.Value, GetCurrentUserRole());
        if (error == null) return NoContent();
        return error == "Member not found in this group." ? NotFound(new { message = error })
             : error == "Forbidden."                      ? Forbid()
             : BadRequest(new { message = error });
    }

    /// <summary>Changes a member's role within the group. Admin+ or GroupAdmin.</summary>
    [HttpPut("{id:int}/members/{targetUserId:int}/role")]
    public async Task<IActionResult> UpdateMemberRole(int id, int targetUserId, [FromBody] UpdateGroupMemberRoleRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _groupService.UpdateMemberRoleAsync(id, targetUserId, request, userId.Value, GetCurrentUserRole());
        if (error == null) return NoContent();
        return error == "Member not found in this group." ? NotFound(new { message = error })
             : error == "Forbidden."                      ? Forbid()
             : BadRequest(new { message = error });
    }
}
