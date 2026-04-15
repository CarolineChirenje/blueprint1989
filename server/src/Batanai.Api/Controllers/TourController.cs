using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.Data;

namespace Batanai.Api.Controllers;

[ApiController]
[Authorize]
public class TourController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TourController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("api/tour/status")]
    public async Task<IActionResult> GetStatus()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Invalid user token" });

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        return Ok(new { completed = user.TourCompletedAt != null, completedAt = user.TourCompletedAt });
    }

    [HttpPost("api/tour/complete")]
    public async Task<IActionResult> Complete()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized(new { message = "Invalid user token" });

        var user = await _context.Users.FindAsync(userId.Value);
        if (user == null) return NotFound();

        user.TourCompletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}
