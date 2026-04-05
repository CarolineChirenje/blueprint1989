using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Batanai.Api.Data;
using Batanai.Api.DTOs.Batanai;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/currencies")]
[Authorize]
public class CurrencyController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CurrencyController(ApplicationDbContext context) => _context = context;

    /// <summary>Returns all active currencies for dropdown selection.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var currencies = await _context.Currencies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.Symbol))
            .ToListAsync();

        return Ok(currencies);
    }
}
