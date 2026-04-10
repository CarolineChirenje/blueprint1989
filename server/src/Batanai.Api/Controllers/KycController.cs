using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.DTOs.Kyc;
using Batanai.Api.Models;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/kyc")]
[Authorize]
public class KycController : ControllerBase
{
    private readonly KycService _kycService;

    public KycController(KycService kycService)
    {
        _kycService = kycService;
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
             : roleStr == nameof(Role.Admin) ? Role.Admin
             : Role.Member;
    }

    private bool IsAdminOrAbove() => GetCurrentUserRole() is Role.SuperAdmin or Role.Admin;

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitKycRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _kycService.SubmitKycAsync(userId.Value, request);
        if (error != null) return BadRequest(new { message = error });
        return Ok(dto);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var dto = await _kycService.GetKycStatusAsync(userId.Value);
        if (dto == null) return NotFound(new { message = "User not found." });
        return Ok(dto);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        if (!IsAdminOrAbove()) return Forbid();
        var items = await _kycService.GetPendingKycAsync();
        return Ok(items);
    }

    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewKycRequest request)
    {
        if (!IsAdminOrAbove()) return Forbid();
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _kycService.ReviewKycAsync(id, userId.Value, request.Approve, request.RejectionReason);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }

    [HttpPost("bypass/{userId:int}")]
    public async Task<IActionResult> Bypass(int userId, [FromBody] BypassKycRequest request)
    {
        if (!IsAdminOrAbove()) return Forbid();
        var adminId = GetCurrentUserId();
        if (adminId == null) return Unauthorized();

        var error = await _kycService.AdminBypassKycAsync(userId, adminId.Value, request.Note);
        if (error != null) return BadRequest(new { message = error });
        return NoContent();
    }
}
