using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Controllers.Api;

/// <summary>
/// REST API controller for scanner device scan submission.
/// Implements US0030 (Scan Ingestion API), US0031 (QR Validation), US0032 (Duplicate Detection).
/// </summary>
[ApiController]
[Route("api/v1/scans")]
[Produces("application/json")]
[EnableCors("ScannerDevices")]
public class ScansApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IQrCodeService _qrCodeService;
    private readonly IDeviceService _deviceService;
    private readonly ICalendarService _calendarService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ISmsService _smsService;
    private readonly ILogger<ScansApiController> _logger;

    public ScansApiController(
        ApplicationDbContext context,
        IQrCodeService qrCodeService,
        IDeviceService deviceService,
        ICalendarService calendarService,
        IAppSettingsService appSettingsService,
        ISmsService smsService,
        ILogger<ScansApiController> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _deviceService = deviceService;
        _calendarService = calendarService;
        _appSettingsService = appSettingsService;
        _smsService = smsService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a scan from a scanner device.
    /// Implements US0030 (Scan Ingestion API).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitScan([FromBody] ScanSubmissionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // US0030-AC2: API Key Authentication
        if (!Request.Headers.TryGetValue("X-API-Key", out var apiKeyHeader) ||
            string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            return Unauthorized(new ErrorResponse
            {
                Error = "InvalidApiKey",
                Message = "Invalid or missing API key"
            });
        }

        var apiKey = apiKeyHeader.ToString();

        // Look up device by API key
        var device = await AuthenticateDeviceAsync(apiKey);
        if (device == null)
        {
            _logger.LogWarning("Invalid API key attempt from {IpAddress}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new ErrorResponse
            {
                Error = "InvalidApiKey",
                Message = "Invalid or missing API key"
            });
        }

        // Check if device is active
        if (!device.IsActive)
        {
            _logger.LogWarning("Revoked device {DeviceId} attempted to submit scan", device.Id);
            return Unauthorized(new ErrorResponse
            {
                Error = "DeviceRevoked",
                Message = "Device has been revoked"
            });
        }

        // Update device last seen
        device.LastSeenAt = DateTime.UtcNow;

        var cameraName = request.CameraName is { Length: > 100 }
            ? request.CameraName[..100]
            : request.CameraName;
        if (request.CameraName is { Length: > 100 })
            _logger.LogWarning("CameraName truncated from {Original} to 100 chars on device {DeviceId}", request.CameraName.Length, device.Id);

        // US0073-AC1: Route by QR prefix — SMARTLOG-V: for visitors, SMARTLOG: for students
        var visitorParsed = _qrCodeService.ParseVisitorQrPayload(request.QrPayload);
        if (visitorParsed != null)
        {
            return await HandleVisitorScanAsync(device, visitorParsed.Value, request, cameraName);
        }

        // US0031-AC1, AC2: Parse and validate QR payload (student)
        var parsed = _qrCodeService.ParseQrPayload(request.QrPayload);
        if (parsed == null)
        {
            _logger.LogWarning("Invalid QR format from device {DeviceId}", device.Id);
            return BadRequest(new ErrorResponse
            {
                Error = "InvalidQrCode",
                Message = "QR code format is invalid",
                Status = "REJECTED"
            });
        }

        var (studentIdStr, timestamp, signature) = parsed.Value;

        // US0031-AC3: Verify HMAC signature
        if (!await _qrCodeService.VerifyQrCodeAsync(request.QrPayload, signature))
        {
            _logger.LogWarning("Invalid QR signature from device {DeviceId}", device.Id);
            await LogRejectedScanAsync(device.Id, null, request, "REJECTED_INVALID_QR");
            return BadRequest(new ErrorResponse
            {
                Error = "InvalidQrCode",
                Message = "QR code signature is invalid",
                Status = "REJECTED"
            });
        }

        // US0031-AC4: Lookup student and QR code
        var student = await _context.Students
            .Include(s => s.QrCodes)
            .FirstOrDefaultAsync(s => s.StudentId == studentIdStr);

        if (student == null)
        {
            _logger.LogWarning("Student not found: {StudentId}", studentIdStr);
            return NotFound(new ErrorResponse
            {
                Error = "StudentNotFound",
                Message = "Student not found",
                Status = "REJECTED"
            });
        }

        var activeQrCode = student.QrCodes.FirstOrDefault(q => q.IsValid);

        // US0031-AC5: Check if QR code is still valid
        if (activeQrCode == null)
        {
            _logger.LogWarning("Invalidated QR code for student {StudentId}", studentIdStr);
            await LogRejectedScanAsync(device.Id, student.Id, request, "REJECTED_QR_INVALIDATED");
            return BadRequest(new ErrorResponse
            {
                Error = "QrCodeInvalidated",
                Message = "QR code has been invalidated. Student needs a new ID card.",
                Status = "REJECTED"
            });
        }

        // US0030-AC6: Check if student is active
        if (!student.IsActive)
        {
            _logger.LogWarning("Inactive student scan attempt: {StudentId}", studentIdStr);
            await LogRejectedScanAsync(device.Id, student.Id, request, "REJECTED_STUDENT_INACTIVE");
            return BadRequest(new ErrorResponse
            {
                Error = "StudentInactive",
                Message = "Student is not active",
                StudentId = studentIdStr,
                Status = "REJECTED"
            });
        }

        // Always derive UTC from the incoming DateTimeOffset before any date/time comparisons
        var scannedAtUtc = request.ScannedAt.UtcDateTime;

        // Calendar Integration: Check if it's a school day (can be disabled via settings)
        var enforceSchoolDay = await _appSettingsService.GetAsync("Attendance.EnforceSchoolDayValidation");
        if (enforceSchoolDay != "false")
        {
            var scanDate = scannedAtUtc.Date;
            var isSchoolDay = await _calendarService.IsSchoolDayAsync(scanDate, student.GradeLevel);
            if (!isSchoolDay)
            {
                var events = await _calendarService.GetEventsForDateAsync(scanDate);
                var eventReason = events.FirstOrDefault(e => e.AffectsAttendance);
                var reasonMessage = eventReason != null
                    ? $"School is closed: {eventReason.Title}"
                    : "Not a school day";

                _logger.LogWarning("Scan rejected - not a school day: {StudentId}, Date: {ScanDate}, Reason: {Reason}",
                    studentIdStr, scanDate, reasonMessage);

                await LogRejectedScanAsync(device.Id, student.Id, request, "REJECTED_NOT_SCHOOL_DAY");

                return BadRequest(new ErrorResponse
                {
                    Error = "NotSchoolDay",
                    Message = reasonMessage,
                    StudentId = studentIdStr,
                    Status = "REJECTED"
                });
            }
        }

        // US0032: Check for duplicate scan (within 5 minutes, same device, same scan type)
        var duplicateScan = await CheckDuplicateScanAsync(device.Id, student.Id, request.ScanType, scannedAtUtc);
        if (duplicateScan != null)
        {
            _logger.LogInformation("Duplicate scan detected for student {StudentId} on device {DeviceId}",
                studentIdStr, device.Id);

            return Ok(new ScanResponse
            {
                ScanId = duplicateScan.Id,
                StudentId = studentIdStr,
                Lrn = student.LRN,
                StudentName = student.FullName,
                Grade = student.GradeLevel,
                Section = student.Section,
                Program = student.Program,
                ScanType = request.ScanType,
                ScannedAt = scannedAtUtc,
                Status = "DUPLICATE",
                OriginalScanId = duplicateScan.Id,
                Message = "Already scanned. Please proceed."
            });
        }

        // US0030-AC4: Create scan record — always store ScannedAt as UTC
        var scan = new Scan
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            StudentId = student.Id,
            QrPayload = request.QrPayload,
            ScannedAt = scannedAtUtc,
            ReceivedAt = DateTime.UtcNow,
            ScanType = request.ScanType,
            Status = "ACCEPTED",
            CameraIndex = request.CameraIndex,
            CameraName = cameraName
        };

        _context.Scans.Add(scan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Scan accepted for student {StudentId} on device {DeviceId}",
            studentIdStr, device.Id);

        // US0054: Queue entry/exit SMS only when opted in (both master switch and per-scan flag must be true)
        if (student.SmsEnabled && student.EntryExitSmsEnabled)
        {
            try
            {
                await _smsService.QueueAttendanceNotificationAsync(student.Id, request.ScanType, scannedAtUtc, scan.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue attendance SMS for student {StudentId}", studentIdStr);
                // SMS failure does not fail the scan
            }
        }

        // US0030-AC3: Return success response
        return Ok(new ScanResponse
        {
            ScanId = scan.Id,
            StudentId = studentIdStr,
            Lrn = student.LRN,
            StudentName = student.FullName,
            Grade = student.GradeLevel,
            Section = student.Section,
            Program = student.Program,
            ScanType = request.ScanType,
            ScannedAt = scannedAtUtc,
            Status = "ACCEPTED"
        });
    }

    /// <summary>
    /// US0073: Handle visitor QR scan — HMAC verify, pass lookup, duplicate check, create VisitorScan.
    /// </summary>
    private async Task<IActionResult> HandleVisitorScanAsync(
        Device device,
        (string Code, long Timestamp, string Signature) visitor,
        ScanSubmissionRequest request,
        string? cameraName)
    {
        // US0073-AC2: HMAC verification
        if (!await _qrCodeService.VerifyVisitorQrAsync(visitor.Code, visitor.Timestamp, visitor.Signature))
        {
            _logger.LogWarning("Invalid visitor QR signature for {Code} from device {DeviceId}", visitor.Code, device.Id);
            return BadRequest(new ErrorResponse
            {
                Error = "InvalidQrCode",
                Message = "QR code signature is invalid",
                Status = "REJECTED"
            });
        }

        // US0073-AC3: Pass lookup
        var pass = await _context.VisitorPasses.FirstOrDefaultAsync(p => p.Code == visitor.Code);
        if (pass == null)
        {
            _logger.LogWarning("Visitor pass not found: {Code}", visitor.Code);
            return BadRequest(new ErrorResponse
            {
                Error = "InvalidQrCode",
                Message = "Visitor pass not found",
                Status = "REJECTED"
            });
        }

        // US0073-AC4: Active check
        if (!pass.IsActive)
        {
            _logger.LogWarning("Inactive visitor pass scanned: {Code}", visitor.Code);
            return BadRequest(new ErrorResponse
            {
                Error = "PassInactive",
                Message = "Visitor pass has been deactivated",
                Status = "REJECTED_PASS_INACTIVE"
            });
        }

        var scannedAtUtc = request.ScannedAt.UtcDateTime;

        // US0073-AC5: Duplicate detection (5-min window)
        var duplicateScan = await CheckVisitorDuplicateScanAsync(pass.Id, request.ScanType, scannedAtUtc);
        if (duplicateScan != null)
        {
            _logger.LogInformation("Duplicate visitor scan for {Code} on device {DeviceId}", visitor.Code, device.Id);
            return Ok(new VisitorScanResponse
            {
                ScanId = duplicateScan.Id,
                PassCode = pass.Code,
                PassNumber = pass.PassNumber,
                ScanType = request.ScanType,
                ScannedAt = scannedAtUtc,
                Status = "DUPLICATE",
                Message = "Already scanned. Please proceed."
            });
        }

        // US0073-AC6/AC7: Create VisitorScan and update pass status
        var visitorScan = new VisitorScan
        {
            Id = Guid.NewGuid(),
            VisitorPassId = pass.Id,
            DeviceId = device.Id,
            ScanType = request.ScanType,
            ScannedAt = scannedAtUtc,
            ReceivedAt = DateTime.UtcNow,
            Status = "ACCEPTED",
            CameraIndex = request.CameraIndex,
            CameraName = cameraName
        };

        // Set current academic year if available
        var currentAy = await _context.AcademicYears.FirstOrDefaultAsync(a => a.IsCurrent);
        if (currentAy != null)
            visitorScan.AcademicYearId = currentAy.Id;

        // Update pass status based on scan type
        pass.CurrentStatus = request.ScanType == "ENTRY" ? "InUse" : "Available";

        _context.VisitorScans.Add(visitorScan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Visitor scan accepted: {Code} {ScanType} on device {DeviceId}",
            visitor.Code, request.ScanType, device.Id);

        // US0073-AC8: No SMS notification for visitors

        // US0073-AC9: Return visitor-specific response
        return Ok(new VisitorScanResponse
        {
            ScanId = visitorScan.Id,
            PassCode = pass.Code,
            PassNumber = pass.PassNumber,
            ScanType = request.ScanType,
            ScannedAt = scannedAtUtc,
            Status = "ACCEPTED"
        });
    }

    private async Task<VisitorScan?> CheckVisitorDuplicateScanAsync(Guid visitorPassId, string scanType, DateTime scannedAt)
    {
        var windowMinutes = await _appSettingsService.GetAsync("QRCode.DuplicateScanWindowMinutes", 5);
        var windowStart = scannedAt.AddMinutes(-windowMinutes);

        return await _context.VisitorScans
            .Where(s => s.VisitorPassId == visitorPassId &&
                       s.ScanType == scanType &&
                       s.ScannedAt >= windowStart &&
                       s.ScannedAt <= scannedAt &&
                       s.Status == "ACCEPTED")
            .OrderByDescending(s => s.ScannedAt)
            .FirstOrDefaultAsync();
    }

    private async Task<Device?> AuthenticateDeviceAsync(string apiKey)
    {
        var keyHash = _deviceService.HashApiKey(apiKey);
        return await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);
    }

    private async Task<Scan?> CheckDuplicateScanAsync(Guid deviceId, Guid studentId, string scanType, DateTime scannedAt)
    {
        // US0032-AC1: Check for existing scan within the window before the current scan time
        var windowMinutes = await _appSettingsService.GetAsync("QRCode.DuplicateScanWindowMinutes", 5);
        var windowStart = scannedAt.AddMinutes(-windowMinutes);

        return await _context.Scans
            .Where(s => s.DeviceId == deviceId &&
                       s.StudentId == studentId &&
                       s.ScanType == scanType &&
                       s.ScannedAt >= windowStart &&
                       s.ScannedAt <= scannedAt &&
                       s.Status == "ACCEPTED")
            .OrderByDescending(s => s.ScannedAt)
            .FirstOrDefaultAsync();
    }

    private async Task LogRejectedScanAsync(Guid deviceId, Guid? studentId, ScanSubmissionRequest request, string status)
    {
        var scan = new Scan
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            StudentId = studentId ?? Guid.Empty, // Use Guid.Empty for invalid student IDs
            QrPayload = request.QrPayload,
            ScannedAt = request.ScannedAt.UtcDateTime,
            ReceivedAt = DateTime.UtcNow,
            ScanType = request.ScanType,
            Status = status
        };

        _context.Scans.Add(scan);
        await _context.SaveChangesAsync();
    }
}

/// <summary>
/// Request model for scan submission.
/// </summary>
public class ScanSubmissionRequest
{
    [Required]
    public string QrPayload { get; set; } = string.Empty;

    /// <summary>
    /// When the QR code was scanned. Scanner sends UTC via DateTimeOffset.UtcNow.
    /// Using DateTimeOffset (not DateTime) so timezone offset is preserved and UTC can be derived unambiguously.
    /// </summary>
    [Required]
    public DateTimeOffset ScannedAt { get; set; }

    [Required]
    [RegularExpression("^(ENTRY|EXIT)$")]
    public string ScanType { get; set; } = string.Empty;

    /// <summary>
    /// 1-based slot index (1..N) of the camera that captured this scan on a multi-camera device.
    /// Omitted or null for single-camera devices and older scanner versions.
    /// </summary>
    [Range(1, 8, ErrorMessage = "CameraIndex must be between 1 and 8")]
    public int? CameraIndex { get; set; }

    /// <summary>
    /// User-assigned name of the camera (e.g. "Main Gate Left").
    /// Omitted or null when not provided. Over-length values are silently truncated to 100 characters.
    /// </summary>
    public string? CameraName { get; set; }
}

/// <summary>
/// Response model for successful scan submission.
/// </summary>
public class ScanResponse
{
    public Guid ScanId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string? Lrn { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string? Program { get; set; }
    public string ScanType { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? OriginalScanId { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Response model for visitor scan submission (US0073-AC9).
/// </summary>
public class VisitorScanResponse
{
    public Guid ScanId { get; set; }
    public string PassCode { get; set; } = string.Empty;
    public int PassNumber { get; set; }
    public string ScanType { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>
/// Error response model.
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? StudentId { get; set; }
    public string? Status { get; set; }
}
