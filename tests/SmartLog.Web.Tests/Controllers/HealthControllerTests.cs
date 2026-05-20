using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Controllers.Api;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Controllers;

/// <summary>
/// Tests for the unified health endpoint (US0121 / PL0041).
/// Covers AC1 through AC10.
/// </summary>
public class HealthControllerTests
{
    private const string ValidApiKey = "sk_live_health";
    private const string ValidApiKeyHash = "hash_health";

    private readonly Mock<IDeviceService> _deviceService = new();
    private readonly Mock<ILogger<HealthController>> _logger = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
    private readonly ApplicationDbContext _context;
    private readonly Device _activeDevice;

    public HealthControllerTests()
    {
        _context = TestDbContextFactory.Create();
        _activeDevice = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Gate Scanner 1",
            Location = "Main Gate",
            ApiKeyHash = ValidApiKeyHash,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow.AddDays(-30),
            RegisteredBy = "admin",
            LastSeenAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.Devices.Add(_activeDevice);
        _context.SaveChanges();

        _deviceService.Setup(d => d.HashApiKey(ValidApiKey)).Returns(ValidApiKeyHash);
        _deviceService.Setup(d => d.HashApiKey(It.Is<string>(s => s != ValidApiKey)))
            .Returns<string>(s => "hash_" + s);
    }

    private HealthController CreateController(string? apiKey = null, ApplicationDbContext? context = null)
    {
        var controller = new HealthController(
            context ?? _context,
            _deviceService.Object,
            _memoryCache,
            _logger.Object);

        var http = new DefaultHttpContext();
        if (apiKey != null)
        {
            http.Request.Headers["X-API-Key"] = apiKey;
        }
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    // ========== AC1: Unauthenticated minimal response ==========

    [Fact]
    public async Task Get_NoHeader_Returns200WithMinimalPayload()
    {
        var controller = CreateController();

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("healthy", body.Status);
        Assert.False(string.IsNullOrEmpty(body.ServerTime));
        Assert.Equal("1.0.0", body.Version);
        Assert.Null(body.Database);
        Assert.Null(body.Scanners);
        Assert.Null(body.Error);
    }

    [Fact]
    public async Task Get_EmptyHeader_TreatedAsMissing()
    {
        var controller = CreateController(apiKey: "");

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Null(body.Database);
        Assert.Null(body.Scanners);
    }

    // ========== AC2: Authenticated full response ==========

    [Fact]
    public async Task Get_ValidKey_Returns200WithFullPayload()
    {
        var controller = CreateController(apiKey: ValidApiKey);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("healthy", body.Status);
        Assert.NotNull(body.Database);
        Assert.Equal("healthy", body.Database!.Status);
        Assert.NotNull(body.Scanners);
        Assert.True(body.Scanners!.Active >= 1);
        Assert.False(string.IsNullOrEmpty(body.ServerTime));
    }

    // ========== AC3: Invalid key -> 401 ==========

    [Fact]
    public async Task Get_InvalidKey_Returns401()
    {
        var controller = CreateController(apiKey: "wrong-key");

        var result = await controller.Get();

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var body = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("InvalidApiKey", body.Error);
    }

    // ========== AC6: No DB hit on unauth path ==========

    [Fact]
    public async Task Get_NoHeader_DoesNotHitDatabase()
    {
        // Disposing the context guarantees any DB access throws — proving the
        // unauth path never queries the database.
        var disposableCtx = TestDbContextFactory.Create();
        await disposableCtx.DisposeAsync();

        var controller = CreateController(context: disposableCtx);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("healthy", body.Status);
    }

    // ========== AC7: LastSeenAt update on auth path ==========

    [Fact]
    public async Task Get_ValidKey_UpdatesLastSeenAt()
    {
        var staleTime = _activeDevice.LastSeenAt;
        var controller = CreateController(apiKey: ValidApiKey);

        await controller.Get();

        var refreshed = await _context.Devices.FindAsync(_activeDevice.Id);
        Assert.NotNull(refreshed);
        Assert.NotEqual(staleTime, refreshed!.LastSeenAt);
        Assert.True(refreshed.LastSeenAt > DateTime.UtcNow.AddSeconds(-5));
    }

    // ========== AC8: 503 with serverTime when DB down on auth path ==========

    [Fact]
    public async Task Get_ValidKey_DbDown_Returns503WithServerTime()
    {
        // Use a context with the device row, then dispose it before the controller call.
        // The auth-classification query (the first DB hit) will throw, triggering the
        // 503 path with serverTime preserved.
        var ctxWithDevice = TestDbContextFactory.Create();
        ctxWithDevice.Devices.Add(new Device
        {
            Id = Guid.NewGuid(),
            Name = "x",
            Location = "x",
            ApiKeyHash = ValidApiKeyHash,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = "x"
        });
        ctxWithDevice.SaveChanges();
        await ctxWithDevice.DisposeAsync();

        var controller = CreateController(apiKey: ValidApiKey, context: ctxWithDevice);

        var result = await controller.Get();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, status.StatusCode);
        var body = Assert.IsType<HealthResponse>(status.Value);
        Assert.Equal("unhealthy", body.Status);
        Assert.False(string.IsNullOrEmpty(body.ServerTime));
        Assert.Equal("1.0.0", body.Version);
        Assert.NotNull(body.Database);
        Assert.Equal("unhealthy", body.Database!.Status);
        Assert.Equal(-1, body.Database.LatencyMs);
    }

    [Fact]
    public async Task Get_NoHeader_DbDown_StillReturns200()
    {
        // Process is alive even if DB is not — the unauth path must not regress.
        var ctx = TestDbContextFactory.Create();
        await ctx.DisposeAsync();
        var controller = CreateController(context: ctx);

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.Equal("healthy", body.Status);
    }

    // ========== AC9: Cache-Control header ==========

    [Fact]
    public async Task Get_NoHeader_HasCacheControlHeader()
    {
        var controller = CreateController();

        await controller.Get();

        var cc = controller.Response.Headers.CacheControl.ToString();
        Assert.Contains("no-cache", cc);
        Assert.Contains("no-store", cc);
    }

    // ========== AC4: serverTime parseable as UTC ==========

    [Fact]
    public async Task Get_ServerTime_IsParseableAsUtc()
    {
        var controller = CreateController();

        var result = await controller.Get();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        var parsed = DateTime.Parse(
            body.ServerTime,
            CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        Assert.True((DateTime.UtcNow - parsed).Duration() < TimeSpan.FromSeconds(5));
        Assert.EndsWith("Z", body.ServerTime);
    }

    // ========== AC5: Shim contracts ==========

#pragma warning disable CS0618 // Calling Obsolete shims is intentional in this test.

    [Fact]
    public async Task GetDetails_Shim_Returns200_AndDelegates()
    {
        var controller = CreateController(apiKey: ValidApiKey);

        var result = await controller.GetDetails();

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<HealthResponse>(ok.Value);
        Assert.NotNull(body.Database);
        Assert.NotNull(body.Scanners);
    }

    [Fact]
    public void GetTime_Shim_ReturnsUtcField()
    {
        var controller = CreateController();

        var result = controller.GetServerTime();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(ok.Value);
        // Verify the anonymous object exposes a "utc" property holding an ISO-8601 string.
        var prop = ok.Value!.GetType().GetProperty("utc");
        Assert.NotNull(prop);
        var utc = prop!.GetValue(ok.Value) as string;
        Assert.False(string.IsNullOrEmpty(utc));
        Assert.EndsWith("Z", utc);
    }

#pragma warning restore CS0618

    // ========== AC10: Repeated invalid key rate-limited logging ==========

    [Fact]
    public async Task Get_RepeatedInvalidKey_LogsOnceWithinWindow()
    {
        var controller = CreateController(apiKey: "wrong-key");

        await controller.Get();
        await controller.Get();
        await controller.Get();

        _logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Invalid API key")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
