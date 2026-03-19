using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Controllers.Api;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Controllers;

public class ScansApiControllerTests
{
    private readonly Mock<IQrCodeService> _qrCodeService = new();
    private readonly Mock<IDeviceService> _deviceService = new();
    private readonly Mock<ICalendarService> _calendarService = new();
    private readonly Mock<ISmsService> _smsService = new();
    private readonly Mock<IAppSettingsService> _appSettingsService = new();
    private readonly Mock<ILogger<ScansApiController>> _logger = new();

    private const string ValidApiKey = "sk_live_test123";
    private const string ValidApiKeyHash = "hashed_test123";
    private const string ValidQrPayload = "SMARTLOG:2026-07-0001:1739512547:BASE64HMAC";
    private const string ValidStudentId = "2026-07-0001";

    private ScansApiController CreateController(HttpContext? httpContext = null)
    {
        var controller = new ScansApiController(
            _context,
            _qrCodeService.Object,
            _deviceService.Object,
            _calendarService.Object,
            _smsService.Object,
            _appSettingsService.Object,
            _logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext ?? CreateHttpContext(ValidApiKey)
        };

        return controller;
    }

    private readonly Data.ApplicationDbContext _context;
    private readonly Device _activeDevice;
    private readonly Student _activeStudent;

    public ScansApiControllerTests()
    {
        _context = TestDbContextFactory.Create();

        // Seed an active device
        _activeDevice = new Device
        {
            Id = Guid.NewGuid(),
            Name = "Gate Scanner 1",
            Location = "Main Gate",
            ApiKeyHash = ValidApiKeyHash,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = "admin"
        };
        _context.Devices.Add(_activeDevice);

        // Seed an active student with valid QR code
        _activeStudent = new Student
        {
            StudentId = ValidStudentId,
            FirstName = "Juan",
            LastName = "Dela Cruz",
            GradeLevel = "7",
            Section = "Section A",
            ParentGuardianName = "Maria Dela Cruz",
            GuardianRelationship = "Mother",
            ParentPhone = "09171234567",
            IsActive = true,
            QrCode = new QrCode
            {
                Payload = ValidQrPayload,
                HmacSignature = "BASE64HMAC",
                IsValid = true
            }
        };
        _context.Students.Add(_activeStudent);
        _context.SaveChanges();

        // Default mock setups
        _deviceService.Setup(d => d.HashApiKey(ValidApiKey)).Returns(ValidApiKeyHash);
        _qrCodeService.Setup(q => q.ParseQrPayload(ValidQrPayload))
            .Returns((ValidStudentId, 1739512547L, "BASE64HMAC"));
        _qrCodeService.Setup(q => q.VerifyQrCodeAsync(ValidQrPayload, "BASE64HMAC"))
            .ReturnsAsync(true);
        _calendarService.Setup(c => c.IsSchoolDayAsync(It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        _appSettingsService.Setup(a => a.GetAsync("QRCode.DuplicateScanWindowMinutes", 5))
            .ReturnsAsync(5);
    }

    private static HttpContext CreateHttpContext(string? apiKey)
    {
        var context = new DefaultHttpContext();
        if (apiKey != null)
        {
            context.Request.Headers["X-API-Key"] = apiKey;
        }
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return context;
    }

    private static ScanSubmissionRequest CreateValidRequest(DateTime? scannedAt = null) => new()
    {
        QrPayload = ValidQrPayload,
        ScannedAt = scannedAt ?? DateTime.UtcNow,
        ScanType = "ENTRY"
    };

    // ========== Authentication Tests ==========

    [Fact]
    public async Task SubmitScan_MissingApiKey_Returns401()
    {
        var controller = CreateController(CreateHttpContext(null));
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("InvalidApiKey", error.Error);
    }

    [Fact]
    public async Task SubmitScan_InvalidApiKey_Returns401()
    {
        _deviceService.Setup(d => d.HashApiKey("wrong-key")).Returns("wrong-hash");
        var controller = CreateController(CreateHttpContext("wrong-key"));
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("InvalidApiKey", error.Error);
    }

    [Fact]
    public async Task SubmitScan_RevokedDevice_Returns401()
    {
        _activeDevice.IsActive = false;
        _context.SaveChanges();

        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("DeviceRevoked", error.Error);
    }

    // ========== QR Validation Tests ==========

    [Fact]
    public async Task SubmitScan_InvalidQrFormat_Returns400()
    {
        _qrCodeService.Setup(q => q.ParseQrPayload("INVALID_PAYLOAD"))
            .Returns((ValueTuple<string, long, string>?)null);

        var controller = CreateController();
        var request = CreateValidRequest();
        request.QrPayload = "INVALID_PAYLOAD";

        var result = await controller.SubmitScan(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("InvalidQrCode", error.Error);
        Assert.Equal("REJECTED", error.Status);
    }

    [Fact]
    public async Task SubmitScan_InvalidHmacSignature_Returns400()
    {
        _qrCodeService.Setup(q => q.VerifyQrCodeAsync(ValidQrPayload, "BASE64HMAC"))
            .ReturnsAsync(false);

        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("InvalidQrCode", error.Error);

        // Should log a rejected scan record
        Assert.Contains(_context.Scans, s => s.Status == "REJECTED_INVALID_QR");
    }

    // ========== Student Lookup Tests ==========

    [Fact]
    public async Task SubmitScan_StudentNotFound_Returns404()
    {
        _qrCodeService.Setup(q => q.ParseQrPayload(ValidQrPayload))
            .Returns(("NONEXISTENT-001", 1739512547L, "BASE64HMAC"));

        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFound.Value);
        Assert.Equal("StudentNotFound", error.Error);
    }

    [Fact]
    public async Task SubmitScan_InactiveStudent_Returns400()
    {
        _activeStudent.IsActive = false;
        _context.SaveChanges();

        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("StudentInactive", error.Error);
        Assert.Equal("REJECTED", error.Status);
    }

    [Fact]
    public async Task SubmitScan_InvalidatedQrCode_Returns400()
    {
        _activeStudent.QrCode!.IsValid = false;
        _context.SaveChanges();

        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("QrCodeInvalidated", error.Error);
    }

    // ========== Calendar Tests ==========

    [Fact]
    public async Task SubmitScan_NotSchoolDay_Returns400()
    {
        _calendarService.Setup(c => c.IsSchoolDayAsync(It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(false);
        _calendarService.Setup(c => c.GetEventsForDateAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<CalendarEvent>
            {
                new()
                {
                    Title = "Christmas Holiday",
                    EventType = EventType.Holiday,
                    AffectsAttendance = true,
                    Category = "Holiday",
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow,
                    CreatedBy = "admin",
                    AcademicYearId = 1
                }
            });

        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("NotSchoolDay", error.Error);
        Assert.Contains("Christmas Holiday", error.Message);
    }

    // ========== Duplicate Detection Tests ==========

    [Fact]
    public async Task SubmitScan_DuplicateWithinWindow_ReturnsDuplicate()
    {
        var scannedAt = DateTime.UtcNow;

        // Pre-seed an existing accepted scan within the duplicate window
        _context.Scans.Add(new Scan
        {
            Id = Guid.NewGuid(),
            DeviceId = _activeDevice.Id,
            StudentId = _activeStudent.Id,
            QrPayload = ValidQrPayload,
            ScannedAt = scannedAt.AddMinutes(-2), // 2 minutes ago (within 5-min window)
            ScanType = "ENTRY",
            Status = "ACCEPTED"
        });
        _context.SaveChanges();

        var controller = CreateController();
        var request = CreateValidRequest(scannedAt);

        var result = await controller.SubmitScan(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ScanResponse>(ok.Value);
        Assert.Equal("DUPLICATE", response.Status);
        Assert.Equal("Juan Dela Cruz", response.StudentName);
    }

    // ========== Successful Scan Tests ==========

    [Fact]
    public async Task SubmitScan_ValidScan_ReturnsAccepted()
    {
        var controller = CreateController();
        var request = CreateValidRequest();

        var result = await controller.SubmitScan(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ScanResponse>(ok.Value);
        Assert.Equal("ACCEPTED", response.Status);
        Assert.Equal(ValidStudentId, response.StudentId);
        Assert.Equal("Juan Dela Cruz", response.StudentName);
        Assert.Equal("7", response.Grade);
        Assert.Equal("Section A", response.Section);
        Assert.Equal("ENTRY", response.ScanType);
    }

    [Fact]
    public async Task SubmitScan_ValidScan_PersistsScanRecord()
    {
        var scannedAt = DateTime.UtcNow;
        var controller = CreateController();
        var request = CreateValidRequest(scannedAt);

        await controller.SubmitScan(request);

        var scan = _context.Scans.FirstOrDefault(s => s.Status == "ACCEPTED");
        Assert.NotNull(scan);
        Assert.Equal(_activeDevice.Id, scan.DeviceId);
        Assert.Equal(_activeStudent.Id, scan.StudentId);
        Assert.Equal("ENTRY", scan.ScanType);
        Assert.Equal(ValidQrPayload, scan.QrPayload);
    }

    [Fact]
    public async Task SubmitScan_ValidScan_UpdatesDeviceLastSeen()
    {
        var beforeSubmit = DateTime.UtcNow;
        var controller = CreateController();
        var request = CreateValidRequest();

        await controller.SubmitScan(request);

        // Reload from context
        var device = _context.Devices.Find(_activeDevice.Id);
        Assert.NotNull(device?.LastSeenAt);
        Assert.True(device.LastSeenAt >= beforeSubmit);
    }

    [Fact]
    public async Task SubmitScan_ExitScan_ReturnsAccepted()
    {
        var controller = CreateController();
        var request = CreateValidRequest();
        request.ScanType = "EXIT";

        var result = await controller.SubmitScan(request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ScanResponse>(ok.Value);
        Assert.Equal("ACCEPTED", response.Status);
        Assert.Equal("EXIT", response.ScanType);
    }

    [Fact]
    public async Task SubmitScan_QueuesSmsNotification()
    {
        var controller = CreateController();
        var request = CreateValidRequest();

        await controller.SubmitScan(request);

        // Give the fire-and-forget task a moment to execute
        await Task.Delay(100);

        _smsService.Verify(
            s => s.QueueAttendanceNotificationAsync(
                _activeStudent.Id,
                "ENTRY",
                It.IsAny<DateTime>()),
            Times.Once);
    }
}
