namespace SmartLog.Web.Services;

public class TimezoneService : ITimezoneService
{
    private readonly TimeZoneInfo _schoolTimeZone;
    private readonly ILogger<TimezoneService> _logger;

    public string IanaTimeZoneId { get; }

    public TimezoneService(IAppSettingsService appSettingsService, ILogger<TimezoneService> logger)
    {
        _logger = logger;
        IanaTimeZoneId = appSettingsService.GetAsync("System.SchoolTimezone").GetAwaiter().GetResult()
                         ?? "Asia/Manila";
        _schoolTimeZone = ResolveTimeZone(IanaTimeZoneId);
    }

    private TimeZoneInfo ResolveTimeZone(string ianaId)
    {
        // Try the configured IANA ID (works on Linux/macOS and Windows with ICU)
        try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
        catch (TimeZoneNotFoundException) { }

        // Windows fallback: Singapore Standard Time covers UTC+8
        try { return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"); }
        catch (TimeZoneNotFoundException) { }

        _logger.LogWarning("Could not find timezone {IanaId}. Creating custom UTC+8 timezone.", ianaId);
        return TimeZoneInfo.CreateCustomTimeZone("UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");
    }

    public DateTime ToPhilippinesTime(DateTime utcDateTime)
    {
        // EF Core returns DateTime values from SQL Server with DateTimeKind.Unspecified.
        // All timestamps are stored as UTC, so treat Unspecified as UTC.
        if (utcDateTime.Kind == DateTimeKind.Local)
        {
            _logger.LogWarning("ToPhilippinesTime called with Local DateTime — converting to UTC first.");
            utcDateTime = utcDateTime.ToUniversalTime();
        }
        else if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _schoolTimeZone);
    }

    public DateTime ToUtc(DateTime localDateTime)
    {
        if (localDateTime.Kind == DateTimeKind.Utc)
        {
            _logger.LogWarning("ToUtc called with UTC DateTime. Returning as-is.");
            return localDateTime;
        }

        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _schoolTimeZone);
    }

    public DateTime GetCurrentPhilippinesTime()
    {
        return ToPhilippinesTime(DateTime.UtcNow);
    }

    public string FormatForDisplay(DateTime utcDateTime, string format = "yyyy-MM-dd hh:mm:ss tt")
    {
        var localTime = ToPhilippinesTime(utcDateTime);
        return localTime.ToString(format);
    }
}
