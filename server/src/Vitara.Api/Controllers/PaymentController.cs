using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Divvy.Api.DTOs.Divvy;
using Divvy.Api.Services;

namespace Divvy.Api.Controllers;

[ApiController]
[Route("api/payments")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly PaymentService _paymentService;

    public PaymentController(PaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("id");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Returns all payments for a given cycle.</summary>
    [HttpGet("by-cycle/{cycleId:int}")]
    public async Task<IActionResult> GetByCycle(int cycleId)
    {
        var payments = await _paymentService.GetForCycleAsync(cycleId);
        return Ok(payments);
    }

    /// <summary>Returns payments made or received by the current user.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var payments = await _paymentService.GetForUserAsync(userId.Value);
        return Ok(payments);
    }

    /// <summary>Returns a single payment by ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var payment = await _paymentService.GetByIdAsync(id);
        if (payment == null) return NotFound(new { message = "Payment not found." });
        return Ok(payment);
    }

    /// <summary>Creates a new payment (Pending status). Notifies the payee.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _paymentService.CreateAsync(userId.Value, request);
        if (error != null)
            return error.Contains("not found") ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return CreatedAtAction(nameof(GetById), new { id = dto!.Id }, dto);
    }

    /// <summary>Payee confirms or rejects a pending payment.</summary>
    [HttpPost("{id:int}/respond")]
    public async Task<IActionResult> Respond(int id, [FromBody] RespondPaymentRequest request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var (dto, error) = await _paymentService.RespondAsync(id, userId.Value, request.Confirm, request.Notes);
        if (error != null)
            return error == "Payment not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return Ok(dto);
    }

    /// <summary>Payer deletes a pending payment.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var error = await _paymentService.DeleteAsync(id, userId.Value);
        if (error != null)
            return error == "Payment not found." ? NotFound(new { message = error }) : BadRequest(new { message = error });

        return NoContent();
    }
}
