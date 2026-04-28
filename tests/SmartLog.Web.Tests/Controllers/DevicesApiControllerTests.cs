using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartLog.Web.Controllers.Api;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Controllers;

public class DevicesApiControllerTests
{
    private static Mock<IDeviceHealthService> DefaultHealthService()
    {
        var mock = new Mock<IDeviceHealthService>();
        mock.Setup(m => m.GetThresholdsAsync()).ReturnsAsync(new DeviceHealthThresholds(120, 600));
        return mock;
    }

    private static DevicesApiController CreateController(
        Data.ApplicationDbContext context,
        IDeviceHealthService? healthService = null)
    {
        healthService ??= DefaultHealthService().Object;
        return new DevicesApiController(context, healthService);
    }

    // ─── GetHealth ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHealth_ReturnsExpectedShape()
    {
        var ctx = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        ctx.Devices.Add(new Device
        {
            Name = "Gate 1",
            Location = "Entrance",
            ApiKeyHash = "hash1",
            IsActive = true,
            LastSeenAt = now.AddSeconds(-10),
            AppVersion = "1.4.2",
            BatteryPercent = 85
        });
        ctx.SaveChanges();

        var controller = CreateController(ctx);
        var result = await controller.GetHealth();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = ok.Value as IEnumerable<object>;
        Assert.NotNull(items);
        Assert.Single(items);
    }

    [Fact]
    public async Task GetHealth_ExcludesRevokedDevices()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Devices.Add(new Device
        {
            Name = "Gate 1",
            Location = "Entrance",
            ApiKeyHash = "hash1",
            IsActive = false, // revoked
            LastSeenAt = DateTime.UtcNow.AddSeconds(-5)
        });
        ctx.SaveChanges();

        var controller = CreateController(ctx);
        var result = await controller.GetHealth();

        var ok = Assert.IsType<OkObjectResult>(result);
        var items = (ok.Value as System.Collections.IEnumerable)!.Cast<object>().ToList();
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetHealth_OnlineDevice_StatusIsOnline()
    {
        var ctx = TestDbContextFactory.Create();
        var now = DateTime.UtcNow;
        ctx.Devices.Add(new Device
        {
            Name = "Gate 1",
            Location = "Entrance",
            ApiKeyHash = "hash1",
            IsActive = true,
            LastSeenAt = now.AddSeconds(-30) // 30s ago, well within 2m Online window
        });
        ctx.SaveChanges();

        var controller = CreateController(ctx);
        var result = await controller.GetHealth();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"status\":\"Online\"", json.Replace(" ", ""));
    }

    [Fact]
    public async Task GetHealth_OfflineDevice_StatusIsOffline()
    {
        var ctx = TestDbContextFactory.Create();
        ctx.Devices.Add(new Device
        {
            Name = "Gate 1",
            Location = "Entrance",
            ApiKeyHash = "hash1",
            IsActive = true,
            LastSeenAt = DateTime.UtcNow.AddMinutes(-30) // 30 min ago, beyond Stale window
        });
        ctx.SaveChanges();

        var controller = CreateController(ctx);
        var result = await controller.GetHealth();

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = System.Text.Json.JsonSerializer.Serialize(ok.Value);
        Assert.Contains("\"status\":\"Offline\"", json.Replace(" ", ""));
    }
}
