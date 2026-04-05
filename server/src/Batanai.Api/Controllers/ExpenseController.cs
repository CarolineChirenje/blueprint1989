using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.DTOs.Batanai;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize]
public class ExpenseController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public ExpenseController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Returns all expenses for a given cycle.</summary>
    [HttpGet("by-cycle/{cycleId:int}")]
    public async Task<IActionResult> GetByCycle(int cycleId)
    {
        var expenses = await _expenseService.GetByCycleAsync(cycleId);
        return Ok(expenses);
    }

    /// <summary>Returns a single expense with its member obligations.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var expense = await _expenseService.GetByIdAsync(id);
        if (expense == null) return NotFound(new { message = "Expense not found." });
        return Ok(expense);
    }

    /// <summary>Returns all unsettled obligations for the current user.</summary>
    [HttpGet("my-obligations")]
    public async Task<IActionResult> GetMyObligations()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var obligations = await _expenseService.GetObligationsForUserAsync(userId.Value);
        return Ok(obligations);
    }

    /// <summary>Creates a new expense. The current user is recorded as the logger.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _expenseService.CreateAsync(userId.Value, request);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = dto!.Id }, dto);
    }

    /// <summary>Updates an expense. Only admins or the original payer may update.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _expenseService.UpdateAsync(id, userId.Value, request);
        if (error != null)
            return error == "Expense not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Ok(dto);
    }

    /// <summary>Deletes an expense and its obligations.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var error = await _expenseService.DeleteAsync(id);
        if (error != null)
            return error == "Expense not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return NoContent();
    }
}
