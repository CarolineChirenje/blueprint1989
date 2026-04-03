using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;
using Divvy.Api.Services;

namespace Divvy.Api.Controllers;

[ApiController]
[Route("api/cycles")]
[Authorize]
public class ExpenseCycleController : ControllerBase
{
    private readonly ExpenseCycleService _cycleService;
    private readonly IGroupService _groupService;

    public ExpenseCycleController(ExpenseCycleService cycleService, IGroupService groupService)
    {
        _cycleService  = cycleService;
        _groupService  = groupService;
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

    private bool IsAdminOrAbove() => GetCurrentUserRole() is Role.SuperAdmin or Role.Admin;

    private async Task<bool> CanManageCycleAsync(int cycleId)
    {
        if (IsAdminOrAbove()) return true;
        var userId = GetCurrentUserId();
        if (userId == null) return false;
        var groupId = await _cycleService.GetCycleGroupIdAsync(cycleId);
        if (groupId == null) return false;
        return await _groupService.IsGroupAdminOfGroupAsync(groupId.Value, userId.Value);
    }

    private async Task<bool> CanManageGroupAsync(int groupId)
    {
        if (IsAdminOrAbove()) return true;
        var userId = GetCurrentUserId();
        if (userId == null) return false;
        return await _groupService.IsGroupAdminOfGroupAsync(groupId, userId.Value);
    }

    /// <summary>Returns all cycles. Admins see all; Members see only their own. Optionally filter by groupId.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? groupId = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var cycles = IsAdminOrAbove()
            ? await _cycleService.GetAllAsync(groupId)
            : await _cycleService.GetForUserAsync(userId.Value, groupId);

        return Ok(cycles);
    }

    /// <summary>Returns full detail for a single cycle including members.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var cycle = await _cycleService.GetByIdAsync(id);
        if (cycle == null) return NotFound(new { message = "Cycle not found." });
        return Ok(cycle);
    }

    /// <summary>Returns the balance breakdown for the current user within a cycle.</summary>
    [HttpGet("{id:int}/balance")]
    public async Task<IActionResult> GetBalance(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var balance = await _cycleService.GetBalanceAsync(id, userId.Value);
        if (balance == null) return NotFound(new { message = "Cycle not found." });
        return Ok(balance);
    }

    /// <summary>Creates a new expense cycle. Admin/SuperAdmin or GroupAdmin of the target group.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCycleRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (!await CanManageGroupAsync(request.GroupId))
            return Forbid();

        var (dto, error) = await _cycleService.CreateAsync(userId.Value, request);
        if (error != null) return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = dto!.Id }, dto);
    }

    /// <summary>Updates cycle name and dates. Admin/SuperAdmin or GroupAdmin of the cycle's group.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseCycleRequest request)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var (dto, error) = await _cycleService.UpdateAsync(id, request);
        if (error != null)
            return error == "Cycle not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Ok(dto);
    }

    /// <summary>Closes a cycle. Admin/SuperAdmin or GroupAdmin of the cycle's group.</summary>
    [HttpPost("{id:int}/close")]
    public async Task<IActionResult> Close(int id)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var error = await _cycleService.CloseAsync(id);
        if (error != null)
            return error == "Cycle not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return NoContent();
    }

    /// <summary>Deletes a cycle and all its expenses. Admin/SuperAdmin or GroupAdmin of the cycle's group.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var error = await _cycleService.DeleteAsync(id);
        if (error != null) return NotFound(new { message = error });

        return NoContent();
    }

    /// <summary>Adds a user to a cycle. Admin/SuperAdmin or GroupAdmin of the cycle's group.</summary>
    [HttpPost("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> AddMember(int id, int userId)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var error = await _cycleService.AddMemberAsync(id, userId);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return NoContent();
    }

    /// <summary>Removes a user from a cycle. Admin/SuperAdmin or GroupAdmin of the cycle's group.</summary>
    [HttpDelete("{id:int}/members/{userId:int}")]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var error = await _cycleService.RemoveMemberAsync(id, userId);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return NoContent();
    }
}

