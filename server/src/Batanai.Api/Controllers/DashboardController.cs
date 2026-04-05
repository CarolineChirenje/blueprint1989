using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Models;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>
    /// Returns a summary of the current user's unsettled obligations
    /// across all active expense cycles.
    /// </summary>
    [HttpGet("obligations-summary")]
    public async Task<IActionResult> GetObligationsSummary()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        // Join unsettled obligations → expenses → active cycles for this user
        var rows = await (
            from o in _context.MemberObligations
            join e in _context.Expenses on o.ExpenseId equals e.Id
            join c in _context.ExpenseCycles on e.ExpenseCycleId equals c.Id
            where o.UserId == userId.Value
               && !o.IsSettled
               && c.Status == CycleStatus.Active
            select new { o.AmountOwed, c.Id, c.Name, c.EndDate }
        ).ToListAsync();

        if (rows.Count == 0)
        {
            return Ok(new ObligationsSummaryDto(0m, 0, 0, null, null));
        }

        var totalOwed       = rows.Sum(r => r.AmountOwed);
        var unsettledCount  = rows.Count;
        var cycleGroups     = rows.GroupBy(r => r.Id).ToList();
        var activeCycleCount = cycleGroups.Count;
        var soonest         = cycleGroups
                                .Select(g => new { g.First().Name, g.First().EndDate })
                                .OrderBy(x => x.EndDate)
                                .First();

        return Ok(new ObligationsSummaryDto(
            totalOwed,
            unsettledCount,
            activeCycleCount,
            soonest.Name,
            soonest.EndDate));
    }
}
