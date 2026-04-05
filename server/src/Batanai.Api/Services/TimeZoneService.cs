using Microsoft.AspNetCore.Http;

namespace Batanai.Api.Services;

/// <summary>
/// Converts UTC DateTimes to the effective timezone for the current request.
/// Resolution order:
///   1. AppConfig "DefaultTimeZone" (if not "UTC") — org-wide override set by admin
///   2. X-Timezone request header — browser-detected IANA timezone sent by the client
///   3. UTC — fallback when no timezone can be determined
/// </summary>
public class TimeZoneService
{
    private readonly AppConfigService _appConfig;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TimeZoneService(AppConfigService appConfig, IHttpContextAccessor httpContextAccessor)
    {
        _appConfig = appConfig;
        _httpContextAccessor = httpContextAccessor;
    }

    private TimeZoneInfo ResolveTimeZone()
    {
        // 1. Admin-configured org-wide timezone from AppConfig table
        var configTz = _appConfig.GetStringAsync("DefaultTimeZone", "UTC").GetAwaiter().GetResult();
        if (!string.IsNullOrWhiteSpace(configTz) &&
            !string.Equals(configTz, "UTC", StringComparison.OrdinalIgnoreCase))
        {
            if (TryFindTimeZone(configTz, out var tz)) return tz!;
        }

        // 2. Browser-detected timezone from X-Timezone request header
        var header = _httpContextAccessor.HttpContext?.Request.Headers["X-Timezone"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header) && TryFindTimeZone(header, out var headerTz))
            return headerTz!;

        // 3. Fallback to UTC
        return TimeZoneInfo.Utc;
    }

    private static bool TryFindTimeZone(string id, out TimeZoneInfo? result)
    {
        try
        {
            result = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            result = null;
            return false;
        }
    }

    /// <summary>
    /// Converts a UTC DateTime to the effective timezone for the current request.
    /// </summary>
    public DateTime ConvertFromUtc(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, ResolveTimeZone());
    }

    /// <summary>
    /// Converts a nullable UTC DateTime to the effective timezone for the current request.
    /// </summary>
    public DateTime? ConvertFromUtc(DateTime? utcDateTime)
    {
        if (!utcDateTime.HasValue) return null;
        return ConvertFromUtc(utcDateTime.Value);
    }

    /// <summary>
    /// Gets the effective timezone for the current request.
    /// </summary>
    public TimeZoneInfo TargetTimeZone => ResolveTimeZone();
}
