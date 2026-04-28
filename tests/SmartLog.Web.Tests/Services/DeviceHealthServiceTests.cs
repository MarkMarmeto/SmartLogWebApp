using Moq;
using SmartLog.Web.Services;

namespace SmartLog.Web.Tests.Services;

public class DeviceHealthServiceTests
{
    // ─── ComputeStatusInternal boundary tests ────────────────────────────────

    [Theory]
    [InlineData(0, "Online")]
    [InlineData(60, "Online")]
    [InlineData(120, "Online")]
    [InlineData(121, "Stale")]
    [InlineData(599, "Stale")]
    [InlineData(600, "Stale")]
    [InlineData(601, "Offline")]
    [InlineData(9999, "Offline")]
    public void ComputeStatus_BoundaryCases(int ageSeconds, string expected)
    {
        var now = new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc);
        var lastSeen = now.AddSeconds(-ageSeconds);
        var status = DeviceHealthService.ComputeStatusInternal(lastSeen, now, new(120, 600));
        Assert.Equal(expected, status.ToString());
    }

    [Fact]
    public void ComputeStatus_NullLastSeen_IsOffline()
    {
        var status = DeviceHealthService.ComputeStatusInternal(null, DateTime.UtcNow, new(120, 600));
        Assert.Equal(DeviceHealthStatus.Offline, status);
    }

    [Fact]
    public void ComputeStatus_FutureLastSeen_IsOnline()
    {
        var now = DateTime.UtcNow;
        var lastSeen = now.AddSeconds(5); // slight clock skew, age is negative
        var status = DeviceHealthService.ComputeStatusInternal(lastSeen, now, new(120, 600));
        Assert.Equal(DeviceHealthStatus.Online, status);
    }

    // ─── GetThresholdsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetThresholdsAsync_UsesAppSettingsWithCustomValues()
    {
        var appSettings = new Mock<IAppSettingsService>();
        appSettings.Setup(m => m.GetAsync("Health:OnlineWindowSeconds", 120)).ReturnsAsync(30);
        appSettings.Setup(m => m.GetAsync("Health:StaleWindowSeconds", 600)).ReturnsAsync(300);

        var svc = new DeviceHealthService(appSettings.Object);
        var t = await svc.GetThresholdsAsync();

        Assert.Equal(30, t.OnlineWindowSeconds);
        Assert.Equal(300, t.StaleWindowSeconds);
    }

    [Fact]
    public async Task GetThresholdsAsync_FallsBackToDefaults()
    {
        var appSettings = new Mock<IAppSettingsService>();
        appSettings.Setup(m => m.GetAsync("Health:OnlineWindowSeconds", 120)).ReturnsAsync(120);
        appSettings.Setup(m => m.GetAsync("Health:StaleWindowSeconds", 600)).ReturnsAsync(600);

        var svc = new DeviceHealthService(appSettings.Object);
        var t = await svc.GetThresholdsAsync();

        Assert.Equal(120, t.OnlineWindowSeconds);
        Assert.Equal(600, t.StaleWindowSeconds);
    }
}
