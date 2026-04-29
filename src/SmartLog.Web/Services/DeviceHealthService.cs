namespace SmartLog.Web.Services;

public interface IDeviceHealthService
{
    DeviceHealthStatus ComputeStatus(DateTime? lastSeenAt, DateTime? nowUtc = null);
    Task<DeviceHealthThresholds> GetThresholdsAsync();
}

public enum DeviceHealthStatus { Online, Stale, Offline }

public record DeviceHealthThresholds(int OnlineWindowSeconds, int StaleWindowSeconds);

/// <summary>
/// US0119 AC4: Computes Online/Stale/Offline status from LastSeenAt age.
/// Thresholds are configurable via AppSettings keys Health:OnlineWindowSeconds and Health:StaleWindowSeconds.
/// </summary>
public class DeviceHealthService : IDeviceHealthService
{
    private readonly IAppSettingsService _appSettings;

    public DeviceHealthService(IAppSettingsService appSettings)
    {
        _appSettings = appSettings;
    }

    public async Task<DeviceHealthThresholds> GetThresholdsAsync()
    {
        var online = await _appSettings.GetAsync("Health:OnlineWindowSeconds", 120);
        var stale = await _appSettings.GetAsync("Health:StaleWindowSeconds", 600);
        return new(online, stale);
    }

    public DeviceHealthStatus ComputeStatus(DateTime? lastSeenAt, DateTime? nowUtc = null)
        => ComputeStatusInternal(lastSeenAt, nowUtc ?? DateTime.UtcNow, new(120, 600));

    public static DeviceHealthStatus ComputeStatusInternal(
        DateTime? lastSeenAt,
        DateTime nowUtc,
        DeviceHealthThresholds t)
    {
        if (lastSeenAt is null) return DeviceHealthStatus.Offline;
        var ageSec = (nowUtc - lastSeenAt.Value).TotalSeconds;
        if (ageSec <= t.OnlineWindowSeconds) return DeviceHealthStatus.Online;
        if (ageSec <= t.StaleWindowSeconds) return DeviceHealthStatus.Stale;
        return DeviceHealthStatus.Offline;
    }
}
