namespace SmartLog.Web.Services;

public interface INoScanAlertService
{
    /// <summary>
    /// Manually triggers the no-scan alert processing.
    /// If force=false and it already ran today, returns a skipped result.
    /// If force=true, runs regardless (per-student idempotency still applies).
    /// </summary>
    Task<NoScanAlertTriggerResult> TriggerNowAsync(bool force = false, CancellationToken ct = default);

    /// <summary>Returns whether the alert has already run today (UTC).</summary>
    Task<bool> HasRunTodayAsync(CancellationToken ct = default);
}

public record NoScanAlertTriggerResult(bool WasSkipped, string Reason, int AlertsQueued = 0);
