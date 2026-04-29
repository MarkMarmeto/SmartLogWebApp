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
        Assert.False(string.IsNullOrEmpty(qr.QrImageBase64));
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

    // --- Visitor QR Tests ---

    [Fact]
    public void ParseVisitorQrPayload_ValidPayload_ReturnsParsedComponents()
    {
        var service = CreateService();
        var result = service.ParseVisitorQrPayload("SMARTLOG-V:VISITOR-001:1739512547:BASE64HMAC");

        Assert.NotNull(result);
        Assert.Equal("VISITOR-001", result.Value.Code);
        Assert.Equal(1739512547L, result.Value.Timestamp);
        Assert.Equal("BASE64HMAC", result.Value.Signature);
    }

    [Fact]
    public void ParseVisitorQrPayload_WrongPrefix_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseVisitorQrPayload("SMARTLOG:VISITOR-001:1739512547:SIG");
        Assert.Null(result);
    }

    [Fact]
    public void ParseVisitorQrPayload_TooFewParts_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseVisitorQrPayload("SMARTLOG-V:VISITOR-001");
        Assert.Null(result);
    }

    [Fact]
    public void ParseVisitorQrPayload_NonNumericTimestamp_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseVisitorQrPayload("SMARTLOG-V:VISITOR-001:notanumber:SIG");
        Assert.Null(result);
    }

    [Fact]
    public void ParseVisitorQrPayload_InvalidCodeCharacters_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseVisitorQrPayload("SMARTLOG-V:VISITOR@001:1739512547:SIG");
        Assert.Null(result);
    }

    [Fact]
    public void ParseVisitorQrPayload_EmptyCode_ReturnsNull()
    {
        var service = CreateService();
        var result = service.ParseVisitorQrPayload("SMARTLOG-V::1739512547:SIG");
        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyVisitorQrAsync_ValidSignature_ReturnsTrue()
    {
        var service = CreateService();

        // Compute a valid HMAC for known data
        var code = "VISITOR-001";
        var timestamp = 1739512547L;
        // Generate QR via the service to get a valid signature
        // We use the same HMAC logic: HMAC-SHA256("{code}:{timestamp}")
        using var hmac = new System.Security.Cryptography.HMACSHA256(
            System.Text.Encoding.UTF8.GetBytes(TestHmacKey));
        var hash = hmac.ComputeHash(
            System.Text.Encoding.UTF8.GetBytes($"{code}:{timestamp}"));
        var validSignature = Convert.ToBase64String(hash);

        var result = await service.VerifyVisitorQrAsync(code, timestamp, validSignature);
        Assert.True(result);
    }

    [Fact]
    public async Task VerifyVisitorQrAsync_InvalidSignature_ReturnsFalse()
    {
        var service = CreateService();

        var result = await service.VerifyVisitorQrAsync("VISITOR-001", 1739512547, "InvalidBase64Sig==");
        Assert.False(result);
    }

    [Fact]
    public async Task VerifyVisitorQrAsync_MalformedBase64_ReturnsFalse()
    {
        var service = CreateService();

        var result = await service.VerifyVisitorQrAsync("VISITOR-001", 1739512547, "not-valid-base64!!!");
        Assert.False(result);
    }
}
