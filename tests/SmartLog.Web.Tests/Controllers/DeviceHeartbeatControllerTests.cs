using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Controllers.Api;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Controllers;

public class DeviceHeartbeatControllerTests
{
    private const string ValidApiKey = "sk_live_heartbeat_test";
    private const string ValidApiKeyHash = "hashed_heartbeat_test";

    private readonly Mock<IDeviceService> _deviceService = new();
    private readonly Mock<ILogger<DeviceHeartbeatController>> _logger = new();

    private DeviceHeartbeatController CreateController(
        Data.ApplicationDbContext context,
        string? apiKeyHeader = ValidApiKey)
    {
        _deviceService.Setup(d => d.HashApiKey(ValidApiKey)).Returns(ValidApiKeyHash);

        var controller = new DeviceHeartbeatController(context, _deviceService.Object, _logger.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContext(apiKeyHeader)
        };
        return controller;
    }

    private static HttpContext CreateHttpContext(string? apiKey)
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        if (apiKey != null)
            ctx.Request.Headers["X-API-Key"] = apiKey;
        return ctx;
    }

    private static Device SeedDevice(Data.ApplicationDbContext ctx, bool isActive = true)
    {
        var device = new Device
        {
            Name = "Test Scanner",
            Location = "Gate 1",
            ApiKeyHash = ValidApiKeyHash,
            IsActive = isActive
        };
        ctx.Devices.Add(device);
        ctx.SaveChanges();
        return device;
    }

    // ─── Auth / rejection paths ──────────────────────────────────────────────

    [Fact]
    public async Task Post_NoApiKeyHeader_Returns401()
    {
        var ctx = TestDbContextFactory.Create();
        var controller = CreateController(ctx, apiKeyHeader: null);

        var result = await controller.Post(new HeartbeatRequest());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Post_UnknownApiKey_Returns401()
    {
        var ctx = TestDbContextFactory.Create();
        _deviceService.Setup(d => d.HashApiKey("unknown_key")).Returns("bad_hash");
        // No device with bad_hash seeded
        var controller = CreateController(ctx, apiKeyHeader: "unknown_key");

        var result = await controller.Post(new HeartbeatRequest());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Post_RevokedDevice_Returns401()
    {
        var ctx = TestDbContextFactory.Create();
        SeedDevice(ctx, isActive: false);
        var controller = CreateController(ctx);

        var result = await controller.Post(new HeartbeatRequest());

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ─── Happy path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Post_ValidRequest_Returns204AndPersistsSnapshot()
    {
        var ctx = TestDbContextFactory.Create();
        var device = SeedDevice(ctx);
        var controller = CreateController(ctx);

        var result = await controller.Post(new HeartbeatRequest
        {
            AppVersion = "1.4.2",
            OsVersion = "Windows 11",
            BatteryPercent = 85,
            IsCharging = true,
            NetworkType = "ETHERNET",
            QueuedScansCount = 0
        });

        Assert.IsType<NoContentResult>(result);
        ctx.Entry(device).Reload();
        Assert.Equal("1.4.2", device.AppVersion);
        Assert.Equal(85, device.BatteryPercent);
        Assert.Equal("ETHERNET", device.NetworkType);
        Assert.True(device.IsCharging);
        Assert.NotNull(device.LastSeenAt);
        Assert.NotNull(device.LastHeartbeatAt);
    }

    [Fact]
    public async Task Post_ValidRequest_DoesNotCreateAuditLogRow()
    {
        var ctx = TestDbContextFactory.Create();
        SeedDevice(ctx);
        var controller = CreateController(ctx);

        await controller.Post(new HeartbeatRequest { AppVersion = "1.0" });

        Assert.Equal(0, ctx.AuditLogs.Count());
    }

    // ─── Field validation / clamping ────────────────────────────────────────

    [Fact]
    public async Task Post_AppVersionOver50Chars_Truncated()
    {
        var ctx = TestDbContextFactory.Create();
        var device = SeedDevice(ctx);
        var controller = CreateController(ctx);
        var longVersion = new string('a', 60);

        await controller.Post(new HeartbeatRequest { AppVersion = longVersion });

        ctx.Entry(device).Reload();
        Assert.Equal(50, device.AppVersion?.Length);
    }

    [Fact]
    public async Task Post_NegativeQueuedScansCount_StoredAsZero()
    {
        var ctx = TestDbContextFactory.Create();
        var device = SeedDevice(ctx);
        var controller = CreateController(ctx);

        await controller.Post(new HeartbeatRequest { QueuedScansCount = -5 });

        ctx.Entry(device).Reload();
        Assert.Equal(0, device.QueuedScansCount);
    }

    [Fact]
    public async Task Post_NullFields_PersistedAsNull()
    {
        var ctx = TestDbContextFactory.Create();
        var device = SeedDevice(ctx);
        var controller = CreateController(ctx);

        await controller.Post(new HeartbeatRequest()); // all null

        ctx.Entry(device).Reload();
        Assert.Null(device.AppVersion);
        Assert.Null(device.BatteryPercent);
        Assert.Null(device.IsCharging);
        Assert.Null(device.NetworkType);
        Assert.Null(device.QueuedScansCount);
    }
}
