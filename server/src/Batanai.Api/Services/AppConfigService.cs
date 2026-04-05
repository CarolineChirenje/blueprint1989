using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Batanai.Api.Data;
using Batanai.Api.DTOs.AppConfig;
using Batanai.Api.Models;

namespace Batanai.Api.Services;

/// <summary>
/// Provides typed, cached access to database-driven application configuration.
/// The full settings dictionary is cached in IMemoryCache with a 5-minute TTL and
/// is explicitly invalidated whenever a value is saved.
/// </summary>
public class AppConfigService
{
    private const string CacheKey = "AppConfig_All";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public AppConfigService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // --- Read helpers ---------------------------------------------------------

    public async Task<string> GetStringAsync(string key, string fallback = "")
    {
        var dict = await GetAllCachedAsync();
        return dict.TryGetValue(key, out var entry) ? entry.Value : fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback = 0)
    {
        var dict = await GetAllCachedAsync();
        if (dict.TryGetValue(key, out var entry) && int.TryParse(entry.Value, out var parsed))
            return parsed;
        return fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback = false)
    {
        var dict = await GetAllCachedAsync();
        if (dict.TryGetValue(key, out var entry) && bool.TryParse(entry.Value, out var parsed))
            return parsed;
        return fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback = 0m)
    {
        var dict = await GetAllCachedAsync();
        if (dict.TryGetValue(key, out var entry) && decimal.TryParse(entry.Value, out var parsed))
            return parsed;
        return fallback;
    }

    // --- CRUD (used by AppConfigController) ----------------------------------

    public async Task<List<AppConfigEntryDto>> GetAllAsync()
    {
        var entries = await _context.AppConfigEntries
            .OrderBy(e => e.Category)
            .ThenBy(e => e.DisplayName)
            .ToListAsync();

        return entries.Select(MapToDto).ToList();
    }

    public async Task<AppConfigEntryDto?> GetByKeyAsync(string key)
    {
        var entry = await _context.AppConfigEntries
            .FirstOrDefaultAsync(e => e.Key == key);

        return entry == null ? null : MapToDto(entry);
    }

    public async Task<(AppConfigEntryDto? dto, string? error)> UpdateAsync(string key, string newValue)
    {
        var entry = await _context.AppConfigEntries.FirstOrDefaultAsync(e => e.Key == key);

        if (entry == null)
            return (null, "Setting not found.");

        if (entry.IsReadOnly)
            return (null, $"Setting '{key}' is read-only and cannot be modified.");

        entry.Value = newValue;
        entry.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        InvalidateCache();

        return (MapToDto(entry), null);
    }

    public async Task<(List<AppConfigEntryDto> updated, List<string> errors)> BulkUpdateAsync(
        IEnumerable<UpdateAppConfigEntryRequest> updates)
    {
        var updatedDtos = new List<AppConfigEntryDto>();
        var errors = new List<string>();

        // Load all keys in one query to avoid N+1
        var keys = updates.Select(u => u.Key).Distinct().ToList();
        var entries = await _context.AppConfigEntries
            .Where(e => keys.Contains(e.Key))
            .ToDictionaryAsync(e => e.Key);

        foreach (var req in updates)
        {
            if (!entries.TryGetValue(req.Key, out var entry))
            {
                errors.Add($"Key '{req.Key}' not found.");
                continue;
            }

            if (entry.IsReadOnly)
            {
                errors.Add($"Key '{req.Key}' is read-only and cannot be modified.");
                continue;
            }

            entry.Value = req.Value;
            entry.UpdatedAt = DateTime.UtcNow;
            updatedDtos.Add(MapToDto(entry));
        }

        if (updatedDtos.Count > 0)
        {
            await _context.SaveChangesAsync();
            InvalidateCache();
        }

        return (updatedDtos, errors);
    }

    // --- Internal helpers -----------------------------------------------------

    private async Task<Dictionary<string, AppConfigEntry>> GetAllCachedAsync()
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, AppConfigEntry>? cached) && cached != null)
            return cached;

        var entries = await _context.AppConfigEntries.AsNoTracking().ToListAsync();
        var dict = entries.ToDictionary(e => e.Key);

        _cache.Set(CacheKey, dict, CacheTtl);
        return dict;
    }

    private void InvalidateCache() => _cache.Remove(CacheKey);

    private static AppConfigEntryDto MapToDto(AppConfigEntry e) => new()
    {
        Id             = e.Id,
        Key            = e.Key,
        Value          = e.Value,
        DataType       = e.DataType,
        Category       = e.Category,
        DisplayName    = e.DisplayName,
        Description    = e.Description,
        IsReadOnly      = e.IsReadOnly,
        RequiresRestart = e.RequiresRestart,
        IsSecret        = e.IsSecret,
        UpdatedAt       = e.UpdatedAt
    };
}
