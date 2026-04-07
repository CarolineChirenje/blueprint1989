using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Models;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/cycles")]
[Authorize]
public class ExpenseCycleController : ControllerBase
{
    private readonly ExpenseCycleService _cycleService;
    private readonly MukandoService      _mukandoService;
    private readonly IGroupService _groupService;
    private readonly IFileStorageService _fileStorage;

    public ExpenseCycleController(ExpenseCycleService cycleService, MukandoService mukandoService, IGroupService groupService, IFileStorageService fileStorage)
    {
        _cycleService   = cycleService;
        _mukandoService = mukandoService;
        _groupService   = groupService;
        _fileStorage    = fileStorage;
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
            ? await _cycleService.GetAllAsync(userId.Value, groupId)
            : await _cycleService.GetForUserAsync(userId.Value, groupId);

        return Ok(cycles);
    }

    /// <summary>Returns full detail for a single cycle including members.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = GetCurrentUserId();
        var cycle = await _cycleService.GetByIdAsync(id, userId ?? 0);
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

    /// <summary>Returns the outstanding balance for the current user across all their active cycles.</summary>
    [HttpGet("outstanding-summary")]
    public async Task<IActionResult> GetOutstandingSummary()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var summary = await _cycleService.GetOutstandingSummaryAsync(userId.Value);
        return Ok(summary);
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

    /// <summary>Starts a Draft cycle. Validates ≥2 members and no open disputes.</summary>
    [HttpPost("{id:int}/start")]
    public async Task<IActionResult> Start(int id)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var userId = GetCurrentUserId();
        var (dto, error) = await _cycleService.StartAsync(id, userId);
        if (error != null)
            return error == "Cycle not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Ok(dto);
    }

    /// <summary>Returns contribution summary: total expenses, share per member, and each member's paid/owed/balance.</summary>
    [HttpGet("{id:int}/contribution-summary")]
    public async Task<IActionResult> GetContributionSummary(int id)
    {
        var summary = await _cycleService.GetContributionSummaryAsync(id);
        if (summary == null) return NotFound(new { message = "Cycle not found." });
        return Ok(summary);
    }

    /// <summary>Sends a personalised payment reminder push to all unsettled members.</summary>
    [HttpPost("{id:int}/send-reminder")]
    public async Task<IActionResult> SendReminder(int id)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var error = await _cycleService.SendReminderAsync(id);
        if (error != null)
            return error == "Cycle not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return NoContent();
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

    /// <summary>Adds multiple users to a cycle in a single operation.</summary>
    [HttpPost("{id:int}/members/batch")]
    public async Task<IActionResult> AddMembersBatch(int id, [FromBody] AddMembersBatchRequest request)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var userId = GetCurrentUserId();
        var (addedUserIds, error) = await _cycleService.AddMembersBatchAsync(id, request.UserIds, userId ?? 0);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Ok(new { addedUserIds });
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

    // ══════════════════════════════════════════════════════════════════════════
    //  Mukando Endpoints
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Returns all rounds for a Mukando cycle.</summary>
    [HttpGet("{id:int}/rounds")]
    public async Task<IActionResult> GetRounds(int id)
    {
        var rounds = await _mukandoService.GetRoundsAsync(id);
        return Ok(rounds);
    }

    /// <summary>Returns round detail with contributions.</summary>
    [HttpGet("{id:int}/rounds/{roundId:int}")]
    public async Task<IActionResult> GetRoundDetail(int id, int roundId)
    {
        var round = await _mukandoService.GetRoundDetailAsync(roundId);
        if (round == null) return NotFound(new { message = "Round not found." });
        return Ok(round);
    }

    /// <summary>Returns the payout record for a round (if exists).</summary>
    [HttpGet("{id:int}/rounds/{roundId:int}/payout")]
    public async Task<IActionResult> GetPayout(int id, int roundId)
    {
        var payout = await _mukandoService.GetPayoutAsync(roundId);
        if (payout == null) return NotFound(new { message = "Payout not found." });
        return Ok(payout);
    }

    /// <summary>Member records their contribution for a round (with proof file).</summary>
    [HttpPost("{id:int}/rounds/{roundId:int}/contribute")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> RecordContribution(int id, int roundId, [FromForm] RecordContributionRequest request, IFormFile? proof)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        // Upload proof file if provided
        string? proofUrl = request.ProofUrl;
        if (proof != null && proof.Length > 0)
        {
            try
            {
                var (fileId, _) = await _fileStorage.UploadAsync(proof, "proofs", userId.Value);
                proofUrl = $"/api/files/{fileId}";
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        var req = request with { ProofUrl = proofUrl ?? "" };
        var error = await _mukandoService.RecordContributionAsync(roundId, userId.Value, req);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Admin confirms a member's contribution.</summary>
    [HttpPost("{id:int}/rounds/{roundId:int}/confirm-contribution/{memberId:int}")]
    public async Task<IActionResult> ConfirmContribution(int id, int roundId, int memberId)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var adminId = GetCurrentUserId();
        if (adminId == null) return Unauthorized();

        var error = await _mukandoService.ConfirmContributionAsync(roundId, memberId, adminId.Value);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Admin records payout to round recipient (with proof).</summary>
    [HttpPost("{id:int}/rounds/{roundId:int}/payout")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> RecordPayout(int id, int roundId, [FromForm] RecordPayoutRequest request, IFormFile? proof)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var adminId = GetCurrentUserId();
        if (adminId == null) return Unauthorized();

        // Upload proof file if provided
        string? proofUrl = request.ProofUrl;
        if (proof != null && proof.Length > 0)
        {
            try
            {
                var (fileId, _) = await _fileStorage.UploadAsync(proof, "proofs", adminId.Value);
                proofUrl = $"/api/files/{fileId}";
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        var req = request with { ProofUrl = proofUrl ?? "" };
        var error = await _mukandoService.RecordPayoutAsync(roundId, adminId.Value, req);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Admin force-closes a round, marking unpaid members as Missed.</summary>
    [HttpPost("{id:int}/rounds/{roundId:int}/force-close")]
    public async Task<IActionResult> ForceCloseRound(int id, int roundId)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var adminId = GetCurrentUserId();
        if (adminId == null) return Unauthorized();

        var error = await _mukandoService.ForceCloseRoundAsync(roundId, adminId.Value);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Update Mukando settings (contribution amount, frequency, payout order) in Draft mode.</summary>
    [HttpPut("{id:int}/mukando-settings")]
    public async Task<IActionResult> UpdateMukandoSettings(int id, [FromBody] UpdateMukandoSettingsRequest request)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var error = await _mukandoService.UpdateSettingsAsync(id, request);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Get Mukando cycle-level statistics and member reliability.</summary>
    [HttpGet("{id:int}/mukando-summary")]
    public async Task<IActionResult> GetMukandoSummary(int id)
    {
        var summary = await _mukandoService.GetCycleSummaryAsync(id);
        if (summary == null) return NotFound(new { message = "Mukando cycle not found." });
        return Ok(summary);
    }

    /// <summary>Get round activity log (timeline).</summary>
    [HttpGet("{id:int}/rounds/{roundId:int}/activity")]
    public async Task<IActionResult> GetRoundActivity(int id, int roundId)
    {
        var activities = await _mukandoService.GetRoundActivitiesAsync(roundId);
        return Ok(activities);
    }

    /// <summary>Export Mukando cycle as CSV.</summary>
    [HttpGet("{id:int}/export")]
    public async Task<IActionResult> ExportCycle(int id)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var csv = await _mukandoService.ExportCycleCsvAsync(id);
        if (csv == null) return NotFound(new { message = "Mukando cycle not found." });

        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"mukando-cycle-{id}.csv");
    }

    /// <summary>Duplicate a cycle to create a new one in Draft status.</summary>
    [HttpPost("{id:int}/duplicate")]
    public async Task<IActionResult> DuplicateCycle(int id, [FromBody] DuplicateCycleRequest request)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _cycleService.DuplicateAsync(id, userId.Value, request.NewStartDate);
        if (error != null) return BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = dto!.Id }, dto);
    }

    /// <summary>Get Mukando dashboard data for the current user.</summary>
    [HttpGet("mukando-dashboard")]
    public async Task<IActionResult> GetMukandoDashboard()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var dashboard = await _mukandoService.GetDashboardAsync(userId.Value);
        return Ok(dashboard);
    }

    // ── Swap Requests ──────────────────────────────────────────────

    /// <summary>List all swap requests for a Mukando cycle.</summary>
    [HttpGet("{id:int}/swap-requests")]
    public async Task<IActionResult> GetSwapRequests(int id)
    {
        var swaps = await _mukandoService.GetSwapRequestsAsync(id);
        return Ok(swaps);
    }

    /// <summary>Member creates a swap request (must be in Draft mode).</summary>
    [HttpPost("{id:int}/swap-requests")]
    public async Task<IActionResult> CreateSwapRequest(int id, [FromBody] CreateSwapRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _mukandoService.CreateSwapRequestAsync(id, userId.Value, request.TargetUserId);
        if (error != null) return BadRequest(new { message = error });
        return Ok(dto);
    }

    /// <summary>Target member responds to a swap request (accept/decline).</summary>
    [HttpPost("{id:int}/swap-requests/{swapId:int}/respond")]
    public async Task<IActionResult> RespondSwapRequest(int id, int swapId, [FromBody] RespondSwapRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _mukandoService.RespondSwapRequestAsync(swapId, userId.Value, request.Accept);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    /// <summary>Requester cancels their own pending swap request.</summary>
    [HttpDelete("{id:int}/swap-requests/{swapId:int}")]
    public async Task<IActionResult> CancelSwapRequest(int id, int swapId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _mukandoService.CancelSwapRequestAsync(swapId, userId.Value);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    // ── Opt-out Requests ───────────────────────────────────────────

    /// <summary>List all opt-out requests for a cycle.</summary>
    [HttpGet("{id:int}/opt-out-requests")]
    public async Task<IActionResult> GetOptOutRequests(int id)
    {
        var requests = await _cycleService.GetOptOutRequestsAsync(id);
        return Ok(requests);
    }

    /// <summary>Member requests to opt out of a cycle (Draft only).</summary>
    [HttpPost("{id:int}/opt-out")]
    public async Task<IActionResult> CreateOptOutRequest(int id, [FromBody] CreateOptOutRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _cycleService.CreateOptOutRequestAsync(id, userId.Value, request.Reason);
        if (error != null) return BadRequest(new { message = error });
        return Ok(dto);
    }

    /// <summary>Admin responds to an opt-out request (approve/reject).</summary>
    [HttpPost("{id:int}/opt-out/{requestId:int}/respond")]
    public async Task<IActionResult> RespondOptOutRequest(int id, int requestId, [FromBody] RespondOptOutRequest request)
    {
        if (!await CanManageCycleAsync(id)) return Forbid();

        var adminId = GetCurrentUserId();
        if (adminId == null) return Unauthorized();

        var error = await _cycleService.RespondOptOutRequestAsync(requestId, adminId.Value, request.Approve);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }
}

