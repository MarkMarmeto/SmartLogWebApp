using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class AttendanceServiceTests
{
    private readonly Mock<ILogger<AttendanceService>> _logger = new();

    private (ApplicationDbContext context, AttendanceService service, Student student, Device device) SetupWithStudent()
    {
        var context = TestDbContextFactory.Create();
        var service = new AttendanceService(context, _logger.Object);

        var device = new Device
        {
            Name = "Gate 1",
            Location = "Main",
            ApiKeyHash = "hash",
            IsActive = true,
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = "admin"
        };
        context.Devices.Add(device);

        var student = TestDbContextFactory.CreateStudent(context, "Juan", "Dela Cruz", "2026-07-0001");

        return (context, service, student, device);
    }

    private static void AddScan(ApplicationDbContext context, Guid studentId, Guid deviceId, DateTime scannedAt, string scanType, string status = "ACCEPTED")
    {
        context.Scans.Add(new Scan
        {
            DeviceId = deviceId,
            StudentId = studentId,
            QrPayload = "SMARTLOG:test:123:sig",
            ScannedAt = scannedAt,
            ScanType = scanType,
            Status = status
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GetAttendanceSummaryAsync_NoScans_AllAbsent()
    {
        var (context, service, student, _) = SetupWithStudent();

        var summary = await service.GetAttendanceSummaryAsync(DateTime.UtcNow.Date);

        Assert.Equal(1, summary.TotalEnrolled);
        Assert.Equal(0, summary.Present);
        Assert.Equal(1, summary.Absent);
        Assert.Equal(0, summary.Departed);
        Assert.Equal(0m, summary.AttendanceRate);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceSummaryAsync_EntryOnly_Present()
    {
        var (context, service, student, device) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        AddScan(context, student.Id, device.Id, today.AddHours(7), "ENTRY");

        var summary = await service.GetAttendanceSummaryAsync(today);

        Assert.Equal(1, summary.Present);
        Assert.Equal(0, summary.Absent);
        Assert.Equal(0, summary.Departed);
        Assert.Equal(100m, summary.AttendanceRate);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceSummaryAsync_EntryAndExit_Departed()
    {
        var (context, service, student, device) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        AddScan(context, student.Id, device.Id, today.AddHours(7), "ENTRY");
        AddScan(context, student.Id, device.Id, today.AddHours(16), "EXIT");

        var summary = await service.GetAttendanceSummaryAsync(today);

        Assert.Equal(0, summary.Present);
        Assert.Equal(0, summary.Absent);
        Assert.Equal(1, summary.Departed);
        Assert.Equal(100m, summary.AttendanceRate);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceSummaryAsync_GradeFilter_FiltersCorrectly()
    {
        var (context, service, student, device) = SetupWithStudent();
        // student is Grade 7

        // Add a Grade 8 student
        TestDbContextFactory.CreateStudent(context, "Maria", "Santos", "2026-08-0001", "8");

        var summary = await service.GetAttendanceSummaryAsync(DateTime.UtcNow.Date, gradeFilter: "7");

        Assert.Equal(1, summary.TotalEnrolled);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceListAsync_StatusFilter_AppliedBeforePagination()
    {
        var (context, service, student, device) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        // student has no scans → Absent
        // Add a second student who is present
        var student2 = TestDbContextFactory.CreateStudent(context, "Maria", "Santos", "2026-07-0002");
        AddScan(context, student2.Id, device.Id, today.AddHours(7), "ENTRY");

        var absentRecords = await service.GetAttendanceListAsync(today, statusFilter: "Absent", pageSize: 50);
        var presentRecords = await service.GetAttendanceListAsync(today, statusFilter: "Present", pageSize: 50);

        Assert.Single(absentRecords);
        Assert.Equal("Juan Dela Cruz", absentRecords[0].FullName);
        Assert.Single(presentRecords);
        Assert.Equal("Maria Santos", presentRecords[0].FullName);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceCountAsync_WithStatusFilter_ReturnsFilteredCount()
    {
        var (context, service, student, device) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        // Add a second student who is present
        var student2 = TestDbContextFactory.CreateStudent(context, "Maria", "Santos", "2026-07-0002");
        AddScan(context, student2.Id, device.Id, today.AddHours(7), "ENTRY");

        var absentCount = await service.GetAttendanceCountAsync(today, statusFilter: "Absent");
        var presentCount = await service.GetAttendanceCountAsync(today, statusFilter: "Present");
        var totalCount = await service.GetAttendanceCountAsync(today);

        Assert.Equal(1, absentCount);
        Assert.Equal(1, presentCount);
        Assert.Equal(2, totalCount);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceListAsync_PageSizeClamped()
    {
        var (context, service, _, _) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        // Request page size > 200 — should be clamped
        var records = await service.GetAttendanceListAsync(today, pageSize: 500);

        // Should not throw and should return results (only 1 student)
        Assert.Single(records);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceSummaryAsync_RejectedScansIgnored()
    {
        var (context, service, student, device) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        AddScan(context, student.Id, device.Id, today.AddHours(7), "ENTRY", "REJECTED_INVALID_QR");

        var summary = await service.GetAttendanceSummaryAsync(today);

        Assert.Equal(0, summary.Present);
        Assert.Equal(1, summary.Absent);

        context.Dispose();
    }

    [Fact]
    public async Task GetAttendanceListAsync_SearchTerm_FiltersStudents()
    {
        var (context, service, student, device) = SetupWithStudent();
        var today = DateTime.UtcNow.Date;

        TestDbContextFactory.CreateStudent(context, "Maria", "Santos", "2026-07-0002");

        var results = await service.GetAttendanceListAsync(today, searchTerm: "Juan");
        Assert.Single(results);
        Assert.Equal("Juan Dela Cruz", results[0].FullName);

        context.Dispose();
    }
}
