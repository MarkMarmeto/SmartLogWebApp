using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

/// <summary>
/// Tests for US0108: Attendance — Non-Graded Filter Handling.
/// Verifies the contract that NG students (Student.Program == null) are excluded
/// by ?program= filter and reachable via ?grade=NG, both via natural SQL semantics.
/// </summary>
public class AttendanceServiceNonGradedTests
{
    private static AttendanceService CreateService(ApplicationDbContext context)
        => new(context, NullLogger<AttendanceService>.Instance);

    private static (Student graded, Student ng) SeedTwoStudents(ApplicationDbContext context)
    {
        TestDbContextFactory.SeedAll(context);
        DbInitializer.SeedNonGradedAsync(context, NullLogger.Instance).GetAwaiter().GetResult();

        // Graded student in Grade 7 / REGULAR section
        var graded = TestDbContextFactory.CreateStudent(context, "Greta", "Graded", "2025-07-3001", "7");
        graded.Program = "REGULAR";

        // NG student in NG / LEVEL 1 section
        var ng = TestDbContextFactory.CreateStudent(context, "Nikko", "NonGrad", "2025-07-3002", "NG", "LEVEL 1");
        ng.Program = null;

        context.SaveChanges();
        return (graded, ng);
    }

    private static Device SeedDevice(ApplicationDbContext context)
    {
        var device = new Device
        {
            Name = "Test Scanner",
            Location = "Main Gate",
            ApiKeyHash = "test-hash",
            IsActive = true,
            RegisteredBy = "test"
        };
        context.Devices.Add(device);
        context.SaveChanges();
        return device;
    }

    private static void AddEntryScan(ApplicationDbContext context, Guid deviceId, Guid studentId, DateTime when)
    {
        context.Scans.Add(new Scan
        {
            DeviceId = deviceId,
            StudentId = studentId,
            QrPayload = $"SMARTLOG:{studentId}:0:test",
            ScannedAt = when,
            ScanType = "ENTRY",
            Status = "ACCEPTED"
        });
        context.SaveChanges();
    }

    [Fact]
    public async Task GetAttendanceSummary_ProgramFilter_ExcludesNGStudents()
    {
        using var context = TestDbContextFactory.Create();
        var (graded, ng) = SeedTwoStudents(context);
        var device = SeedDevice(context);
        var today = DateTime.Today.AddHours(8);
        AddEntryScan(context, device.Id, graded.Id, today);
        AddEntryScan(context, device.Id, ng.Id, today);

        var service = CreateService(context);
        var summary = await service.GetAttendanceSummaryAsync(today, programFilter: "REGULAR");

        Assert.Equal(1, summary.TotalEnrolled); // only graded student counted; NG excluded
        Assert.Equal(1, summary.Present);
    }

    [Fact]
    public async Task GetAttendanceSummary_NoFilter_IncludesNGStudents()
    {
        using var context = TestDbContextFactory.Create();
        var (graded, ng) = SeedTwoStudents(context);
        var device = SeedDevice(context);
        var today = DateTime.Today.AddHours(8);
        AddEntryScan(context, device.Id, graded.Id, today);
        AddEntryScan(context, device.Id, ng.Id, today);

        var service = CreateService(context);
        var summary = await service.GetAttendanceSummaryAsync(today);

        Assert.Equal(2, summary.TotalEnrolled);
        Assert.Equal(2, summary.Present);
    }

    [Fact]
    public async Task GetAttendanceList_GradeFilterNG_ReturnsOnlyNGStudents()
    {
        using var context = TestDbContextFactory.Create();
        var (graded, ng) = SeedTwoStudents(context);
        var today = DateTime.Today;

        var service = CreateService(context);
        var records = await service.GetAttendanceListAsync(today, gradeFilter: "NG");

        Assert.Single(records);
        Assert.Equal(ng.Id, records[0].StudentId); // StudentAttendanceRecord.StudentId is the Guid PK
    }

    [Fact]
    public async Task GetAttendanceList_ProgramAndGradeNG_ReturnsEmpty()
    {
        using var context = TestDbContextFactory.Create();
        SeedTwoStudents(context);
        var today = DateTime.Today;

        var service = CreateService(context);
        var records = await service.GetAttendanceListAsync(today, gradeFilter: "NG", programFilter: "REGULAR");

        Assert.Empty(records);
    }
}
