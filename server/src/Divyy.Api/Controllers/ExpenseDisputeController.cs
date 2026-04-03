using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Models;
using Divvy.Api.Services;

namespace Divvy.Api.Controllers;

[ApiController]
[Route("api/disputes")]
[Authorize]
public class ExpenseDisputeController : ControllerBase
{
    private readonly ExpenseDisputeService _disputeService;
    private readonly ExpenseCycleService   _cycleService;
    private readonly IGroupService         _groupService;

    public ExpenseDisputeController(
        ExpenseDisputeService disputeService,
        ExpenseCycleService cycleService,
        IGroupService groupService)
    {
        _disputeService = disputeService;
        _cycleService   = cycleService;
        _groupService   = groupService;
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

    /// <summary>Returns all disputes for a cycle. Any cycle member or admin can view.</summary>
    [HttpGet]
    public async Task<IActionResult> GetByCycle([FromQuery] int cycleId)
    {
        var disputes = await _disputeService.GetByCycleAsync(cycleId);
        return Ok(disputes);
    }

    /// <summary>Raises a dispute on an expense. Cycle must be in Draft status.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDisputeRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _disputeService.CreateAsync(userId.Value, request);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Created(string.Empty, dto);
    }

    /// <summary>Updates dispute status (Reviewed/Resolved/Rejected). Admin/SuperAdmin or GroupAdmin of the cycle.</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateDisputeStatusRequest request)
    {
        if (!IsAdminOrAbove())
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized();
            var canManage = await _disputeService.CanUserManageDisputeAsync(id, userId.Value, _cycleService, _groupService);
            if (!canManage) return Forbid();
        }

        var (dto, error) = await _disputeService.UpdateStatusAsync(id, request);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Ok(dto);
    }
}
