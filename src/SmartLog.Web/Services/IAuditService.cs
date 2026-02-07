namespace SmartLog.Web.Services;

/// <summary>
/// Service for logging audit trail events.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string? userId = null,
        string? performedByUserId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null);
}
