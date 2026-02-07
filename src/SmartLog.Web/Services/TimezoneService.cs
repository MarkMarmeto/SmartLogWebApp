namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of timezone service for Philippines Time (UTC+8) handling.
/// </summary>
public class TimezoneService : ITimezoneService
{
    private readonly TimeZoneInfo _philippinesTimeZone;
    private readonly ILogger<TimezoneService> _logger;

    public TimezoneService(ILogger<TimezoneService> logger)
    {
        _logger = logger;

        // Try to get the timezone info - Asia/Manila for Linux/macOS, Singapore Standard Time for Windows
        try
        {
            _philippinesTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Fallback for Windows
                _philippinesTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Last resort: create a custom timezone for UTC+8
                _logger.LogWarning("Could not find Asia/Manila or Singapore Standard Time timezone. Creating custom UTC+8 timezone.");
                _philippinesTimeZone = TimeZoneInfo.CreateCustomTimeZone(
                    "Philippines Standard Time",
                    TimeSpan.FromHours(8),
                    "Philippines Standard Time",
                    "Philippines Standard Time");
            }
        }
    }

    public DateTime ToPhilippinesTime(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            _logger.LogWarning("ToPhilippinesTime called with non-UTC DateTime. Converting to UTC first.");
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _philippinesTimeZone);
    }

    public DateTime ToUtc(DateTime philippinesDateTime)
    {
        if (philippinesDateTime.Kind == DateTimeKind.Utc)
        {
            _logger.LogWarning("ToUtc called with UTC DateTime. Returning as-is.");
            return philippinesDateTime;
        }

        // Treat as unspecified and convert from Philippines timezone
        var unspecified = DateTime.SpecifyKind(philippinesDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _philippinesTimeZone);
    }

    public DateTime GetCurrentPhilippinesTime()
    {
        return ToPhilippinesTime(DateTime.UtcNow);
    }

    public string FormatForDisplay(DateTime utcDateTime, string format = "yyyy-MM-dd hh:mm:ss tt")
    {
        var philippinesTime = ToPhilippinesTime(utcDateTime);
        return philippinesTime.ToString(format);
    }
}
