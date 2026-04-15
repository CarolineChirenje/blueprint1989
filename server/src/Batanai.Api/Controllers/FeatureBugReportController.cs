using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.Models;
using Batanai.Api.DTOs.FeatureBugReport;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/feature-bug-reports")]
[Authorize]
[Tags("FeatureBugReport")]
public class FeatureBugReportController : ControllerBase
{
    private readonly FeatureBugReportService _service;

    public FeatureBugReportController(FeatureBugReportService service)
    {
        _service = service;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst("role")?.Value ?? string.Empty;
    }

    private bool IsAdminOrSuperAdmin()
    {
        var role = GetCurrentUserRole();
        return role == "SuperAdmin" || role == "Admin";
    }

    // ── Admin endpoints ────────────────────────────────────────────────────

    /// <summary>List all reports with optional filters (Admin/SuperAdmin only).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] ReportStatus? status,
        [FromQuery] ReportType? type,
        [FromQuery] ReportPriority? priority)
    {
        if (!IsAdminOrSuperAdmin()) return Forbid();
        var reports = await _service.GetFilteredAsync(status, type, priority);
        return Ok(reports);
    }

    /// <summary>Get a single report by id (Admin/SuperAdmin only).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!IsAdminOrSuperAdmin()) return Forbid();
        var report = await _service.GetByIdAsync(id);
        if (report == null) return NotFound(new { message = "Report not found." });
        return Ok(report);
    }

    /// <summary>Update report status and optionally set version (Admin/SuperAdmin only).</summary>
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateReportStatusRequest request)
    {
        if (!IsAdminOrSuperAdmin()) return Forbid();
        var result = await _service.UpdateStatusAsync(id, request);
        if (result == null) return NotFound(new { message = "Report not found." });
        return Ok(result);
    }

    // ── User endpoints ─────────────────────────────────────────────────────

    /// <summary>Submit a new feature request or bug report with optional screenshot.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateFeatureBugReportRequest request, IFormFile? image)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { message = "Title and description are required." });

        if (request.Categories == null || request.Categories.Count == 0)
            return BadRequest(new { message = "At least one category is required." });

        try
        {
            var report = await _service.CreateAsync(userId.Value, request, image);
            return CreatedAtAction(nameof(GetById), new { id = report.Id }, report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Get the current user's own reports.</summary>
    [HttpGet("my-reports")]
    public async Task<IActionResult> GetMyReports()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var reports = await _service.GetMyReportsAsync(userId.Value);
        return Ok(reports);
    }

    /// <summary>Get a specific report owned by the current user.</summary>
    [HttpGet("my-reports/{id:int}")]
    public async Task<IActionResult> GetMyReportById(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        var report = await _service.GetByIdAsync(id);
        if (report == null || report.SubmittedByUserId != userId.Value)
            return NotFound(new { message = "Report not found." });
        return Ok(report);
    }

    /// <summary>Update own report (only if status is New).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFeatureBugReportRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var result = await _service.UpdateAsync(id, userId.Value, request);
        if (result == null)
            return BadRequest(new { message = "Report not found, not yours, or already under review." });
        return Ok(result);
    }

    /// <summary>Delete own report (only if status is New).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var deleted = await _service.DeleteAsync(id, userId.Value);
        if (!deleted)
            return BadRequest(new { message = "Report not found, not yours, or already under review." });
        return NoContent();
    }
}
