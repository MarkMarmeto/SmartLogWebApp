using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models.Sms;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

/// <summary>
/// US0086: Calendar-driven No-Scan Alert suppression tests.
/// </summary>
public class NoScanAlertCalendarSuppressionTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Sms:NoScanAlertTime"] = "18:10" })
            .Build();

    private static IServiceProvider BuildServiceProvider(
        Data.ApplicationDbContext context,
        Mock<ICalendarService> calendarService,
        Mock<ISmsTemplateService>? templateService = null,
        Mock<ISmsSettingsService>? smsSettings = null,
        Mock<IAppSettingsService>? appSettings = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton<ICalendarService>(calendarService.Object);
        services.AddSingleton<ISmsTemplateService>((templateService ?? DefaultTemplate()).Object);
        services.AddSingleton<ISmsSettingsService>((smsSettings ?? DefaultSmsSettings()).Object);
        services.AddSingleton<IAppSettingsService>((appSettings ?? DefaultAppSettings()).Object);
        return services.BuildServiceProvider();
    }

    private static NoScanAlertService CreateService(IServiceProvider sp) =>
        new NoScanAlertService(sp, new Mock<ILogger<NoScanAlertService>>().Object, BuildConfig());

    private static Mock<ISmsTemplateService> DefaultTemplate()
    {
        var mock = new Mock<ISmsTemplateService>();
        mock.Setup(m => m.RenderTemplateAsync("NO_SCAN_ALERT", It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync("Alert message");
        return mock;
    }

    private static Mock<ISmsSettingsService> DefaultSmsSettings()
    {
        var mock = new Mock<ISmsSettingsService>();
        mock.Setup(m => m.IsSmsEnabledAsync()).ReturnsAsync(true);
        return mock;
    }

    private static Mock<IAppSettingsService> DefaultAppSettings()
    {
        var mock = new Mock<IAppSettingsService>();
        mock.Setup(m => m.GetAsync("Sms:NoScanAlertEnabled")).ReturnsAsync("true");
        mock.Setup(m => m.GetAsync("Sms:NoScanAlertProvider")).ReturnsAsync((string?)null);
        return mock;
    }

    private static Mock<ICalendarService> SchoolDayCalendar(List<AlertSuppression>? suppressions = null)
    {
        var mock = new Mock<ICalendarService>();
        mock.Setup(m => m.IsSchoolDayAsync(It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(true);
        mock.Setup(m => m.GetTodaysSuppressionsAsync(It.IsAny<DateOnly>()))
            .ReturnsAsync(suppressions ?? new List<AlertSuppression>());
        return mock;
    }

    private static AcademicYear SeedCurrentYear(Data.ApplicationDbContext context)
    {
        var year = new AcademicYear
        {
            Name = "2025-2026",
            StartDate = new DateTime(2025, 6, 1),
            EndDate = new DateTime(2026, 3, 31),
            IsCurrent = true,
            IsActive = true
        };
        context.AcademicYears.Add(year);
        context.SaveChanges();
        return year;
    }

    private static Student SeedStudentWithScan(Data.ApplicationDbContext context, AcademicYear year,
        string firstName, string gradeLevel)
    {
        var student = SeedStudent(context, year, firstName, gradeLevel: gradeLevel);
        SeedScan(context, student.Id, DateTime.Today.AddHours(8));
        return student;
    }

    private static Student SeedStudent(Data.ApplicationDbContext context, AcademicYear year,
        string firstName = "Test", string gradeLevel = "7", bool smsEnabled = true)
    {
        var student = new Student
        {
            StudentId = $"SL-2026-{Guid.NewGuid().ToString()[..5]}",
            FirstName = firstName,
            LastName = "Student",
            GradeLevel = gradeLevel,
            Section = "A",
            ParentGuardianName = "Parent",
            GuardianRelationship = "Mother",
            ParentPhone = "09170000001",
            IsActive = true,
            SmsEnabled = smsEnabled,
            SmsLanguage = "EN"
        };
        context.Students.Add(student);

        var section = new Section { Name = "A", IsActive = true };
        context.Sections.Add(section);
        context.SaveChanges();

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = section.Id,
            AcademicYearId = year.Id,
            IsActive = true
        });
        context.SaveChanges();
        return student;
    }

    private static void SeedScan(Data.ApplicationDbContext context, Guid studentId, DateTime scannedAt)
    {
        var device = new Device { Name = "Gate", ApiKeyHash = Guid.NewGuid().ToString(), IsActive = true };
        context.Devices.Add(device);
        context.SaveChanges();

        context.Scans.Add(new Scan
        {
            StudentId = studentId,
            DeviceId = device.Id,
            QrPayload = "SMARTLOG:test",
            ScanType = "ENTRY",
            Status = "ACCEPTED",
            ScannedAt = scannedAt,
            ReceivedAt = scannedAt
        });
        context.SaveChanges();
    }

    // ─── System-Wide Suppression ──────────────────────────────────────────────

    [Fact]
    public async Task SystemWideSuppression_NoAlertsQueued_AuditLogWritten()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        // Seed a student with no scan (would normally trigger alert)
        SeedStudent(context, year, firstName: "Maria");
        // Seed another student with scan to pass health guard
        var other = SeedStudent(context, year, firstName: "Pedro");
        SeedScan(context, other.Id, DateTime.Today.AddHours(8));

        var suppression = new AlertSuppression { Reason = "Independence Day", GradeLevels = new List<string>() };
        var calendar = SchoolDayCalendar(new List<AlertSuppression> { suppression });

        var sp = BuildServiceProvider(context, calendar);
        var service = CreateService(sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
        var auditLog = context.AuditLogs.FirstOrDefault(a => a.Action == "NO_SCAN_ALERT_SUPPRESSED");
        Assert.NotNull(auditLog);
        Assert.Contains("Independence Day", auditLog.Details);
    }

    [Fact]
    public async Task SystemWideSuppression_AuditLogContainsSuppressedByCalendarEvent()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        var other = SeedStudent(context, year, firstName: "Pedro");
        SeedScan(context, other.Id, DateTime.Today.AddHours(8));

        var suppression = new AlertSuppression { Reason = "Sports Day", GradeLevels = new List<string>() };
        var calendar = SchoolDayCalendar(new List<AlertSuppression> { suppression });

        var sp = BuildServiceProvider(context, calendar);
        await CreateService(sp).InvokeAlertForTestAsync();

        var log = context.AuditLogs.Single(a => a.Action == "NO_SCAN_ALERT_SUPPRESSED");
        Assert.Contains("Suppressed by calendar event: Sports Day", log.Details);
    }

    // ─── Per-Grade Suppression ─────────────────────────────────────────────────

    [Fact]
    public async Task PerGradeSuppression_SuppressedGradeStudentsSkipped_OtherGradesAlerted()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);

        // Grade 7 student — no scan (would normally get alert)
        var grade7Student = SeedStudent(context, year, firstName: "Ana", gradeLevel: "7");

        // Grade 8 student — no scan (should still get alert)
        var grade8Student = SeedStudent(context, year, firstName: "Bob", gradeLevel: "8");

        // Third student with scan to pass scanner health guard
        var withScan = SeedStudent(context, year, firstName: "Carlos", gradeLevel: "9");
        SeedScan(context, withScan.Id, DateTime.Today.AddHours(8));

        // Grade 7 is suppressed (school event)
        var suppression = new AlertSuppression { Reason = "Grade 7 Field Trip", GradeLevels = new List<string> { "7" } };
        var calendar = SchoolDayCalendar(new List<AlertSuppression> { suppression });

        var sp = BuildServiceProvider(context, calendar);
        await CreateService(sp).InvokeAlertForTestAsync();

        var queued = context.SmsQueues.ToList();
        // Only grade 8 student should receive an alert
        Assert.Single(queued);
        Assert.Equal(grade8Student.ParentPhone, queued[0].PhoneNumber);
    }

    [Fact]
    public async Task PerGradeSuppression_MultipleSuppressedGrades_AllSkipped()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);

        var grade7 = SeedStudent(context, year, firstName: "Ana", gradeLevel: "7");
        var grade8 = SeedStudent(context, year, firstName: "Bob", gradeLevel: "8");
        var grade9 = SeedStudent(context, year, firstName: "Carlos", gradeLevel: "9");
        SeedScan(context, grade9.Id, DateTime.Today.AddHours(8));

        var suppression = new AlertSuppression { Reason = "Exams", GradeLevels = new List<string> { "7", "8" } };
        var calendar = SchoolDayCalendar(new List<AlertSuppression> { suppression });

        var sp = BuildServiceProvider(context, calendar);
        await CreateService(sp).InvokeAlertForTestAsync();

        // Grade 9 has a scan, grades 7 and 8 are suppressed → 0 alerts
        Assert.Equal(0, context.SmsQueues.Count());
    }

    [Fact]
    public async Task PerGradeSuppression_AuditLogMentionsSuppressedGrades()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);

        var grade7 = SeedStudent(context, year, firstName: "Ana", gradeLevel: "7");
        var withScan = SeedStudent(context, year, firstName: "Carlos", gradeLevel: "9");
        SeedScan(context, withScan.Id, DateTime.Today.AddHours(8));
        // Grade 9 has no scan — but wait, withScan DID scan. So grade 9 student has a scan and won't alert.
        // Ana (grade 7) has no scan but grade 7 is suppressed.
        // All students will be skipped: grade 7 by suppression, grade 9 by having a scan.
        // 0 alerts queued, but audit log should mention grade 7 suppression.

        var suppression = new AlertSuppression { Reason = "Field Trip", GradeLevels = new List<string> { "7" } };
        var calendar = SchoolDayCalendar(new List<AlertSuppression> { suppression });

        var sp = BuildServiceProvider(context, calendar);
        await CreateService(sp).InvokeAlertForTestAsync();

        var log = context.AuditLogs.FirstOrDefault(a => a.Action == "NO_SCAN_ALERT_EXECUTED");
        Assert.NotNull(log);
        Assert.Contains("Grades suppressed by calendar: 7", log.Details);
    }

    // ─── No Suppression (Event without SuppressesNoScanAlert) ─────────────────

    [Fact]
    public async Task NoSuppression_RegularSchoolDay_AlertsQueuedNormally()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);

        var noScanStudent = SeedStudent(context, year, firstName: "Maria");
        var withScan = SeedStudent(context, year, firstName: "Pedro");
        SeedScan(context, withScan.Id, DateTime.Today.AddHours(8));

        // No suppressions — normal school day
        var calendar = SchoolDayCalendar(new List<AlertSuppression>());

        var sp = BuildServiceProvider(context, calendar);
        await CreateService(sp).InvokeAlertForTestAsync();

        Assert.Single(context.SmsQueues.ToList());
    }
}
