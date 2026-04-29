using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Branding;

namespace SmartLog.Web.Tests.Services.Branding;

public class BrandingServiceTests : IDisposable
{
    private readonly Mock<IAppSettingsService> _appSettings = new();
    private readonly Mock<ILogger<BrandingService>> _logger = new();
    private readonly Mock<IWebHostEnvironment> _env = new();
    private readonly string _tempRoot;
    private readonly BrandingService _sut;

    public BrandingServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"BrandingTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
        _env.Setup(e => e.WebRootPath).Returns(_tempRoot);
        _sut = new BrandingService(_env.Object, _appSettings.Object, _logger.Object);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    // ── ValidateLogoAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ValidateLogoAsync_ValidPng_ReturnsTrue()
    {
        var file = MakePng("logo.png", 1024);
        var (isValid, error) = await _sut.ValidateLogoAsync(file);
        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public async Task ValidateLogoAsync_OversizedFile_ReturnsFalse()
    {
        var file = MakePng("large.png", 3 * 1024 * 1024);
        var (isValid, error) = await _sut.ValidateLogoAsync(file);
        Assert.False(isValid);
        Assert.Contains("2 MB", error);
    }

    [Fact]
    public async Task ValidateLogoAsync_WrongExtension_ReturnsFalse()
    {
        var file = MakeFile("file.pdf", "application/pdf", [0x25, 0x50, 0x44, 0x46]);
        var (isValid, error) = await _sut.ValidateLogoAsync(file);
        Assert.False(isValid);
        Assert.Contains("PNG", error);
    }

    [Fact]
    public async Task ValidateLogoAsync_SvgWithScriptTag_ReturnsFalse()
    {
        var svg = "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>";
        var file = MakeSvg("evil.svg", svg);
        var (isValid, error) = await _sut.ValidateLogoAsync(file);
        Assert.False(isValid);
        Assert.Contains("unsafe", error);
    }

    [Fact]
    public async Task ValidateLogoAsync_SvgWithOnloadAttr_ReturnsFalse()
    {
        var svg = "<svg onload=\"alert(1)\" xmlns='http://www.w3.org/2000/svg'></svg>";
        var file = MakeSvg("onload.svg", svg);
        var (isValid, error) = await _sut.ValidateLogoAsync(file);
        Assert.False(isValid);
    }

    [Fact]
    public async Task ValidateLogoAsync_CleanSvg_ReturnsTrue()
    {
        var svg = "<svg xmlns='http://www.w3.org/2000/svg'><circle cx='50' cy='50' r='40'/></svg>";
        var file = MakeSvg("logo.svg", svg);
        var (isValid, _) = await _sut.ValidateLogoAsync(file);
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateLogoAsync_ExecutableDisguisedAsPng_ReturnsFalse()
    {
        // Windows PE/MZ header disguised as a PNG
        var exeBytes = new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03 };
        var file = MakeFile("malicious.png", "image/png", exeBytes);
        var (isValid, error) = await _sut.ValidateLogoAsync(file);
        Assert.False(isValid);
        Assert.Contains("content", error);
    }

    // ── UploadLogoAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task UploadLogoAsync_HappyPath_WritesFileAndPersistsPath()
    {
        _appSettings.Setup(s => s.GetAsync("Branding:SchoolLogoPath")).ReturnsAsync((string?)null);
        _appSettings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), false, null))
            .Returns(Task.CompletedTask);

        var file = MakePng("logo.png", 512);
        var path = await _sut.UploadLogoAsync(file, "admin");

        Assert.Equal("/branding/school-logo.png", path);
        Assert.True(File.Exists(Path.Combine(_tempRoot, "branding", "school-logo.png")));
        _appSettings.Verify(s => s.SetAsync("Branding:SchoolLogoPath", "/branding/school-logo.png", "Branding", "admin", false, null), Times.Once);
    }

    [Fact]
    public async Task UploadLogoAsync_ChangingExtension_DeletesOldFile()
    {
        // Seed old logo
        var brandingDir = Path.Combine(_tempRoot, "branding");
        Directory.CreateDirectory(brandingDir);
        var oldFile = Path.Combine(brandingDir, "school-logo.png");
        await File.WriteAllBytesAsync(oldFile, [0x89, 0x50, 0x4E, 0x47]);

        _appSettings.Setup(s => s.GetAsync("Branding:SchoolLogoPath")).ReturnsAsync("/branding/school-logo.png");
        _appSettings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), false, null))
            .Returns(Task.CompletedTask);

        var svgContent = "<svg xmlns='http://www.w3.org/2000/svg'><circle cx='50' cy='50' r='40'/></svg>";
        var file = MakeSvg("new.svg", svgContent);
        await _sut.UploadLogoAsync(file, "admin");

        Assert.False(File.Exists(oldFile), "Old PNG should have been deleted");
        Assert.True(File.Exists(Path.Combine(brandingDir, "school-logo.svg")));
    }

    // ── RemoveLogoAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task RemoveLogoAsync_DeletesFileAndClearsSetting()
    {
        var brandingDir = Path.Combine(_tempRoot, "branding");
        Directory.CreateDirectory(brandingDir);
        var logoFile = Path.Combine(brandingDir, "school-logo.png");
        await File.WriteAllBytesAsync(logoFile, [0x89, 0x50, 0x4E, 0x47]);

        _appSettings.Setup(s => s.GetAsync("Branding:SchoolLogoPath")).ReturnsAsync("/branding/school-logo.png");
        _appSettings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), false, null))
            .Returns(Task.CompletedTask);

        await _sut.RemoveLogoAsync("admin");

        Assert.False(File.Exists(logoFile));
        _appSettings.Verify(s => s.SetAsync("Branding:SchoolLogoPath", null, "Branding", "admin", false, null), Times.Once);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static IFormFile MakePng(string name, int size)
    {
        var header = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var content = new byte[size];
        Array.Copy(header, content, Math.Min(header.Length, size));
        return MakeFile(name, "image/png", content);
    }

    private static IFormFile MakeSvg(string name, string svgContent)
    {
        var bytes = Encoding.UTF8.GetBytes(svgContent);
        return MakeFile(name, "image/svg+xml", bytes);
    }

    private static IFormFile MakeFile(string name, string contentType, byte[] content)
    {
        var stream = new MemoryStream(content);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns(name);
        file.Setup(f => f.ContentType).Returns(contentType);
        file.Setup(f => f.Length).Returns(content.Length);
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, CancellationToken>((s, _) => new MemoryStream(content).CopyTo(s))
            .Returns(Task.CompletedTask);
        return file.Object;
    }
}
