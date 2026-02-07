namespace SmartLog.Web.Services;

/// <summary>
/// Service for timezone conversions and Philippine Time (UTC+8) handling.
/// </summary>
public interface ITimezoneService
{
    /// <summary>
    /// Converts UTC DateTime to Philippines Time (UTC+8).
    /// </summary>
    DateTime ToPhilippinesTime(DateTime utcDateTime);

    /// <summary>
    /// Converts Philippines Time to UTC.
    /// </summary>
    DateTime ToUtc(DateTime philippinesDateTime);

    /// <summary>
    /// Gets the current time in Philippines timezone.
    /// </summary>
    DateTime GetCurrentPhilippinesTime();

    /// <summary>
    /// Formats a UTC DateTime for display in Philippines Time.
    /// </summary>
    string FormatForDisplay(DateTime utcDateTime, string format = "yyyy-MM-dd hh:mm:ss tt");
}
