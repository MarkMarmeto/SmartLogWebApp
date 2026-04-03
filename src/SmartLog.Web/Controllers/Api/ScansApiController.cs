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
    private readonly ISmsService _smsService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ILogger<ScansApiController> _logger;

    public ScansApiController(
        ApplicationDbContext context,
        IQrCodeService qrCodeService,
        IDeviceService deviceService,
        ICalendarService calendarService,
        ISmsService smsService,
        IAppSettingsService appSettingsService,
        ILogger<ScansApiController> logger)
    {
        _context = context;
        _qrCodeService = qrCodeService;
        _deviceService = deviceService;
        _calendarService = calendarService;
        _smsService = smsService;
        _appSettingsService = appSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a scan from a scanner device.
    /// Implements US0030 (Scan Ingestion API).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SubmitScan([FromBody] ScanSubmissionRequest request)
    {
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

        // US0031-AC1, AC2: Parse and validate QR payload
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
            .Include(s => s.QrCode)
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

        // US0031-AC5: Check if QR code is still valid
        if (student.QrCode == null || !student.QrCode.IsValid)
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

        // Calendar Integration: Check if it's a school day (can be disabled via settings)
        var enforceSchoolDay = await _appSettingsService.GetAsync("Attendance.EnforceSchoolDayValidation");
        if (enforceSchoolDay != "false")
        {
            var scanDate = request.ScannedAt.Date;
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
        var duplicateScan = await CheckDuplicateScanAsync(device.Id, student.Id, request.ScanType, request.ScannedAt);
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
                ScanType = request.ScanType,
                ScannedAt = request.ScannedAt,
                Status = "DUPLICATE",
                OriginalScanId = duplicateScan.Id,
                Message = "Already scanned. Please proceed."
            });
        }

        // US0030-AC4: Create scan record
        var scan = new Scan
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            StudentId = student.Id,
            QrPayload = request.QrPayload,
            ScannedAt = request.ScannedAt,
            ReceivedAt = DateTime.UtcNow,
            ScanType = request.ScanType,
            Status = "ACCEPTED"
        };

        _context.Scans.Add(scan);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Scan accepted for student {StudentId} on device {DeviceId}",
            studentIdStr, device.Id);

        // Queue SMS notification in background (don't await to avoid blocking API response)
        _ = Task.Run(async () =>
        {
            try
            {
                await _smsService.QueueAttendanceNotificationAsync(
                    student.Id,
                    request.ScanType,
                    request.ScannedAt,
                    scan.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error queuing SMS notification for student {StudentId}", studentIdStr);
            }
        });

        // US0030-AC3: Return success response
        return Ok(new ScanResponse
        {
            ScanId = scan.Id,
            StudentId = studentIdStr,
            Lrn = student.LRN,
            StudentName = student.FullName,
            Grade = student.GradeLevel,
            Section = student.Section,
            ScanType = request.ScanType,
            ScannedAt = request.ScannedAt,
            Status = "ACCEPTED"
        });
    }

    private async Task<Device?> AuthenticateDeviceAsync(string apiKey)
    {
        var keyHash = _deviceService.HashApiKey(apiKey);
        return await _context.Devices.FirstOrDefaultAsync(d => d.ApiKeyHash == keyHash);
    }

    private async Task<Scan?> CheckDuplicateScanAsync(Guid deviceId, Guid studentId, string scanType, DateTime scannedAt)
    {
        // US0032-AC1: Check for scan within configurable window
        var windowMinutes = await _appSettingsService.GetAsync("QRCode.DuplicateScanWindowMinutes", 5);
        var fiveMinutesAgo = scannedAt.AddMinutes(-windowMinutes);
        var fiveMinutesLater = scannedAt.AddMinutes(windowMinutes);

        return await _context.Scans
            .Where(s => s.DeviceId == deviceId &&
                       s.StudentId == studentId &&
                       s.ScanType == scanType &&
                       s.ScannedAt >= fiveMinutesAgo &&
                       s.ScannedAt <= fiveMinutesLater &&
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
            ScannedAt = request.ScannedAt,
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

    [Required]
    public DateTime ScannedAt { get; set; }

    [Required]
    [RegularExpression("^(ENTRY|EXIT)$")]
    public string ScanType { get; set; } = string.Empty;
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
    public string ScanType { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? OriginalScanId { get; set; }
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
