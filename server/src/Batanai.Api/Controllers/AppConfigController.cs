using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Batanai.Api.DTOs.AppConfig;
using Batanai.Api.Models;
using Batanai.Api.Services;

namespace Batanai.Api.Controllers;

[ApiController]
[Route("api/app-config")]
[Authorize]
public class AppConfigController : ControllerBase
{
    private readonly AppConfigService _appConfig;

    public AppConfigController(AppConfigService appConfig)
    {
        _appConfig = appConfig;
    }

    /// <summary>
    /// Returns the current frontend and backend version strings.
    /// Public endpoint — no authentication required.
    /// </summary>
    [HttpGet("version")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any, NoStore = false)]
    public async Task<IActionResult> GetVersions()
    {
        var dto = new AppVersionDto
        {
            FrontendVersion = await _appConfig.GetStringAsync(AppConfigKeys.FrontendVersion, "1.0.0"),
            BackendVersion  = await _appConfig.GetStringAsync(AppConfigKeys.BackendVersion,  "1.0.0")
        };
        return Ok(dto);
    }

    /// <summary>
    /// Returns all configuration entries, ordered by category then display name.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> GetAll()
    {
        var entries = await _appConfig.GetAllAsync();
        return Ok(entries);
    }

    /// <summary>
    /// Returns a single configuration entry by its key.
    /// Any authenticated user can read config values (needed by non-admin UI screens).
    /// </summary>
    [HttpGet("{key}")]
    public async Task<IActionResult> GetByKey(string key)
    {
        var entry = await _appConfig.GetByKeyAsync(key);
        if (entry == null)
            return NotFound(new { message = $"Setting '{key}' not found." });

        return Ok(entry);
    }

    /// <summary>
    /// Updates the value of a single configuration entry.
    /// Returns 400 if the key does not exist or is read-only.
    /// </summary>
    [HttpPut("{key}")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateAppConfigEntryRequest request)
    {
        if (key != request.Key)
            return BadRequest(new { message = "Route key and body key do not match." });

        var (dto, error) = await _appConfig.UpdateAsync(key, request.Value);
        if (error != null)
            return BadRequest(new { message = error });

        return Ok(dto);
    }

    /// <summary>
    /// Atomically updates multiple configuration values in one request.
    /// Any read-only or missing keys are reported in the errors list; valid keys are still saved.
    /// </summary>
    [HttpPut("bulk")]
    [Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateAppConfigRequest request)
    {
        var (updated, errors) = await _appConfig.BulkUpdateAsync(request.Updates);

        return Ok(new
        {
            updated,
            errors,
            hasErrors = errors.Count > 0
        });
    }
}
