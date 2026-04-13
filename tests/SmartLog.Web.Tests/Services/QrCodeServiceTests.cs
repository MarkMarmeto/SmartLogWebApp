using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Services;

namespace SmartLog.Web.Tests.Services;

public class QrCodeServiceTests
{
    private readonly Mock<IAppSettingsService> _appSettings = new();
    private readonly Mock<ILogger<QrCodeService>> _logger = new();
    private readonly IConfiguration _configuration;
    private const string TestHmacKey = "test-hmac-secret-key-for-unit-tests";

    public QrCodeServiceTests()
    {
        _appSettings.Setup(s => s.GetAsync("QRCode.HmacSecretKey"))
            .ReturnsAsync(TestHmacKey);

        _configuration = new ConfigurationBuilder().Build();
    }

    private QrCodeService CreateService() =>
        new(_appSettings.Object, _configuration, _logger.Object);

    [Fact]
    public async Task GenerateQrCodeAsync_ReturnsValidQrCode()
    {
        var service = CreateService();
        var qr = await service.GenerateQrCodeAsync("2026-07-0001");

        Assert.NotNull(qr);
        Assert.StartsWith("SMARTLOG:2026-07-0001:", qr.Payload);
        Assert.True(qr.IsValid);
        Assert.NotEmpty(qr.HmacSignature);
        Assert.NotEmpty(qr.QrImageBase64);
    }

    [Fact]
    public async Task GenerateQrCodeAsync_PayloadHasCorrectFormat()
    {
        var service = CreateService();
        var qr = await service.GenerateQrCodeAsync("2026-07-0001");

        var parts = qr.Payload.Split(':');
        Assert.Equal(4, parts.Length);
        Assert.Equal("SMARTLOG", parts[0]);
        Assert.Equal("2026-07-0001", parts[1]);
        Assert.True(long.TryParse(parts[2], out _));
        Assert.Equal(qr.HmacSignature, parts[3]);
    }

    [Fact]
    public async Task VerifyQrCodeAsync_ValidSignature_ReturnsTrue()
    {
        var service = CreateService();
        var qr = await service.GenerateQrCodeAsync("2026-07-0001");

        var result = await service.VerifyQrCodeAsync(qr.Payload, qr.HmacSignature);
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyQrCodeAsync_InvalidSignature_ReturnsFalse()
    {
        var service = CreateService();
        var qr = await service.GenerateQrCodeAsync("2026-07-0001");

        var result = await service.VerifyQrCodeAsync(qr.Payload, "InvalidBase64Signature==");
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyQrCodeAsync_MalformedPayload_ReturnsFalse()
    {
        var service = CreateService();
        var result = await service.VerifyQrCodeAsync("NOT_A_QR_CODE", "sig");
        Assert.False(result);
    }

    [Fact]
    public void ParseQrPayload_ValidPayload_ReturnsParsedComponents()
    {
        var service = CreateService();
        var result = service.ParseQrPayload("SMARTLOG:2026-07-0001:1739512547:BASE64HMAC");

        Assert.NotNull(result);
        Assert.Equal("2026-07-0001", result.Value.StudentId);
        Assert.Equal(1739512547L, result.Value.Timestamp);
        Assert.Equal("BASE64HMAC", result.Value.Signature);
    }

    [Fact]
    public void ParseQrPayload_InvalidPrefix_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseQrPayload("INVALID:2026-07-0001:1739512547:SIG");
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrPayload_TooFewParts_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseQrPayload("SMARTLOG:2026-07-0001");
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrPayload_NonNumericTimestamp_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseQrPayload("SMARTLOG:2026-07-0001:notanumber:SIG");
        Assert.Null(result);
    }
}
