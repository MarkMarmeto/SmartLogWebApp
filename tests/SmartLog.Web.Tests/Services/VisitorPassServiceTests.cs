using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class VisitorPassServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IAppSettingsService> _appSettings = new();
    private readonly Mock<ITimezoneService> _timezoneService = new();
    private readonly Mock<ILogger<VisitorPassService>> _logger = new();
    private readonly IConfiguration _configuration;
    private const string TestHmacKey = "test-hmac-secret-key-for-unit-tests";

    public VisitorPassServiceTests()
    {
        _context = TestDbContextFactory.Create();

        _appSettings.Setup(s => s.GetAsync("QRCode.HmacSecretKey"))
            .ReturnsAsync(TestHmacKey);

        _appSettings.Setup(s => s.GetAsync("Visitor:MaxPasses", 20))
            .ReturnsAsync(20);

        _configuration = new ConfigurationBuilder().Build();
    }

    private VisitorPassService CreateService() =>
        new(_context, _appSettings.Object, _configuration, _timezoneService.Object, _logger.Object);

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task GeneratePassesAsync_CreatesCorrectNumberOfPasses()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        Assert.Equal(20, passes.Count);
    }

    [Fact]
    public async Task GeneratePassesAsync_CodesFormattedCorrectly()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        Assert.Equal("VISITOR-001", passes[0].Code);
        Assert.Equal("VISITOR-010", passes[9].Code);
        Assert.Equal("VISITOR-020", passes[19].Code);
    }

    [Fact]
    public async Task GeneratePassesAsync_QrPayloadStartsWithVisitorPrefix()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        foreach (var pass in passes)
        {
            Assert.StartsWith("SMARTLOG-V:", pass.QrPayload);
        }
    }

    [Fact]
    public async Task GeneratePassesAsync_QrPayloadHasCorrectFormat()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        foreach (var pass in passes)
        {
            var parts = pass.QrPayload.Split(':');
            Assert.Equal(4, parts.Length);
            Assert.Equal("SMARTLOG-V", parts[0]);
            Assert.Equal(pass.Code, parts[1]);
            Assert.True(long.TryParse(parts[2], out _));
            Assert.Equal(pass.HmacSignature, parts[3]);
        }
    }

    [Fact]
    public async Task GeneratePassesAsync_AllPassesAreActiveAndAvailable()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        foreach (var pass in passes)
        {
            Assert.True(pass.IsActive);
            Assert.Equal("Available", pass.CurrentStatus);
        }
    }

    [Fact]
    public async Task GeneratePassesAsync_GeneratesQrImages()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        foreach (var pass in passes)
        {
            Assert.NotNull(pass.QrImageBase64);
            Assert.NotEmpty(pass.QrImageBase64);
        }
    }

    [Fact]
    public async Task GeneratePassesAsync_WhenAllExist_IsNoop()
    {
        var service = CreateService();

        await service.GeneratePassesAsync();
        var passes = await service.GeneratePassesAsync();

        Assert.Equal(20, passes.Count);
        Assert.Equal(20, _context.VisitorPasses.Count());
    }

    [Fact]
    public async Task GeneratePassesAsync_WhenIncreased_GeneratesOnlyNewPasses()
    {
        _appSettings.Setup(s => s.GetAsync("Visitor:MaxPasses", 20))
            .ReturnsAsync(5);
        var service = CreateService();
        await service.GeneratePassesAsync();

        // Increase to 10
        _appSettings.Setup(s => s.GetAsync("Visitor:MaxPasses", 20))
            .ReturnsAsync(10);
        var passes = await service.GeneratePassesAsync();

        Assert.Equal(10, passes.Count);
        Assert.Equal(10, _context.VisitorPasses.Count());
    }

    [Fact]
    public async Task GeneratePassesAsync_PassNumbersAreSequential()
    {
        var service = CreateService();

        var passes = await service.GeneratePassesAsync();

        for (var i = 0; i < passes.Count; i++)
        {
            Assert.Equal(i + 1, passes[i].PassNumber);
        }
    }

    [Fact]
    public async Task DeactivatePassAsync_SetsInactiveAndDeactivated()
    {
        var service = CreateService();
        var passes = await service.GeneratePassesAsync();
        var passId = passes[0].Id;

        await service.DeactivatePassAsync(passId);

        var pass = await service.GetByCodeAsync("VISITOR-001");
        Assert.NotNull(pass);
        Assert.False(pass.IsActive);
        Assert.Equal("Deactivated", pass.CurrentStatus);
    }

    [Fact]
    public async Task ActivatePassAsync_SetsActiveAndAvailable()
    {
        var service = CreateService();
        var passes = await service.GeneratePassesAsync();
        var passId = passes[0].Id;

        await service.DeactivatePassAsync(passId);
        await service.ActivatePassAsync(passId);

        var pass = await service.GetByCodeAsync("VISITOR-001");
        Assert.NotNull(pass);
        Assert.True(pass.IsActive);
        Assert.Equal("Available", pass.CurrentStatus);
    }

    [Fact]
    public async Task DeactivatePassAsync_InvalidId_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeactivatePassAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsCorrectPass()
    {
        var service = CreateService();
        await service.GeneratePassesAsync();

        var pass = await service.GetByCodeAsync("VISITOR-005");

        Assert.NotNull(pass);
        Assert.Equal(5, pass.PassNumber);
        Assert.Equal("VISITOR-005", pass.Code);
    }

    [Fact]
    public async Task GetByCodeAsync_NonExistentCode_ReturnsNull()
    {
        var service = CreateService();

        var pass = await service.GetByCodeAsync("VISITOR-999");

        Assert.Null(pass);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByPassNumber()
    {
        var service = CreateService();
        await service.GeneratePassesAsync();

        var passes = await service.GetAllAsync();

        Assert.Equal(20, passes.Count);
        for (var i = 1; i < passes.Count; i++)
        {
            Assert.True(passes[i].PassNumber > passes[i - 1].PassNumber);
        }
    }

    [Fact]
    public async Task SyncPassCountAsync_WhenDecreased_DeactivatesExcess()
    {
        var service = CreateService();
        await service.GeneratePassesAsync();

        // Decrease to 15
        _appSettings.Setup(s => s.GetAsync("Visitor:MaxPasses", 20))
            .ReturnsAsync(15);
        await service.SyncPassCountAsync();

        var passes = await service.GetAllAsync();
        var activePasses = passes.Where(p => p.IsActive).ToList();
        var deactivatedPasses = passes.Where(p => !p.IsActive).ToList();

        Assert.Equal(15, activePasses.Count);
        Assert.Equal(5, deactivatedPasses.Count);
        Assert.All(deactivatedPasses, p => Assert.True(p.PassNumber > 15));
    }

    [Fact]
    public async Task SetMaxPassesAsync_ZeroOrNegative_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SetMaxPassesAsync(0));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SetMaxPassesAsync(-1));
    }

    [Fact]
    public async Task SetMaxPassesAsync_ValidCount_SavesToAppSettings()
    {
        var service = CreateService();

        await service.SetMaxPassesAsync(50);

        _appSettings.Verify(s => s.SetAsync(
            "Visitor:MaxPasses", "50", "Visitor",
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>()), Times.Once);
    }
}
