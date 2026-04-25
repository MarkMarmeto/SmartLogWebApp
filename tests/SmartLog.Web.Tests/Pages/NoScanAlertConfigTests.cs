using SmartLog.Web.Data.Entities;
using SmartLog.Web.Pages.Admin.Sms;

namespace SmartLog.Web.Tests.Pages;

/// <summary>
/// Tests for US0053: No-Scan Alert Admin Configuration & Dashboard.
/// Covers NoScanAlertRunStatus.FromAuditLog (dashboard card logic) and
/// time validation used in SettingsModel.OnPostAsync.
/// </summary>
public class NoScanAlertConfigTests
{
    // ─── NoScanAlertRunStatus.FromAuditLog ────────────────────────────────────

    [Fact]
    public void FromAuditLog_NullLog_ReturnsHasRunFalse()
    {
        var status = NoScanAlertRunStatus.FromAuditLog(null);

        Assert.False(status.HasRun);
        Assert.False(status.WasSuppressed);
        Assert.Null(status.RunAt);
        Assert.Equal(0, status.AlertsQueued);
    }

    [Fact]
    public void FromAuditLog_ExecutedLog_ParsesAlertsQueued()
    {
        var log = new AuditLog
        {
            Action = "NO_SCAN_ALERT_EXECUTED",
            Details = "Date: 2026-04-16. Alerts queued: 45. Duration: 312ms.",
            Timestamp = new DateTime(2026, 4, 16, 10, 10, 0, DateTimeKind.Utc)
        };

        var status = NoScanAlertRunStatus.FromAuditLog(log);

        Assert.True(status.HasRun);
        Assert.False(status.WasSuppressed);
        Assert.Equal(45, status.AlertsQueued);
        Assert.Equal(log.Timestamp, status.RunAt);
    }

    [Fact]
    public void FromAuditLog_ExecutedLog_ZeroAlerts_ParsesZero()
    {
        var log = new AuditLog
        {
            Action = "NO_SCAN_ALERT_EXECUTED",
            Details = "Date: 2026-04-16. Alerts queued: 0. Duration: 10ms.",
            Timestamp = DateTime.UtcNow
        };

        var status = NoScanAlertRunStatus.FromAuditLog(log);

        Assert.True(status.HasRun);
        Assert.False(status.WasSuppressed);
        Assert.Equal(0, status.AlertsQueued);
    }

    [Fact]
    public void FromAuditLog_SuppressedLog_SetsSuppressedFlag()
    {
        var log = new AuditLog
        {
            Action = "NO_SCAN_ALERT_SUPPRESSED",
            Details = "Date: 2026-04-16. Suppressed: zero total scans today.",
            Timestamp = DateTime.UtcNow
        };

        var status = NoScanAlertRunStatus.FromAuditLog(log);

        Assert.True(status.HasRun);
        Assert.True(status.WasSuppressed);
        Assert.Equal(0, status.AlertsQueued); // no count for suppressed
        Assert.Equal(log.Timestamp, status.RunAt);
    }

    [Fact]
    public void FromAuditLog_ExecutedLog_NullDetails_ReturnsZeroAlerts()
    {
        var log = new AuditLog
        {
            Action = "NO_SCAN_ALERT_EXECUTED",
            Details = null,
            Timestamp = DateTime.UtcNow
        };

        var status = NoScanAlertRunStatus.FromAuditLog(log);

        Assert.True(status.HasRun);
        Assert.Equal(0, status.AlertsQueued);
    }

    // ─── Time validation (mirrors SettingsModel.OnPostAsync logic) ────────────

    [Theory]
    [InlineData("18:10", true)]
    [InlineData("00:00", true)]
    [InlineData("23:59", true)]
    [InlineData("17:30", true)]
    public void TimeOnly_ValidFormats_ParseSuccessfully(string time, bool expected)
    {
        Assert.Equal(expected, TimeOnly.TryParse(time, out _));
    }

    [Theory]
    [InlineData("25:00", false)]
    [InlineData("", false)]
    [InlineData("not-a-time", false)]
    [InlineData("99:99", false)]
    public void TimeOnly_InvalidFormats_FailParsing(string time, bool expected)
    {
        Assert.Equal(expected, TimeOnly.TryParse(time, out _));
    }
}
