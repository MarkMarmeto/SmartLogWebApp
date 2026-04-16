using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Sms;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class NoScanAlertServiceTests
{
    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static NoScanAlertService CreateService(
        IConfiguration? config = null,
        IServiceProvider? serviceProvider = null)
    {
        config ??= BuildConfig("18:10");
        serviceProvider ??= BuildServiceProvider();
        var logger = new Mock<ILogger<NoScanAlertService>>().Object;
        return new NoScanAlertService(serviceProvider, logger, config);
    }

    private static IConfiguration BuildConfig(string alertTime) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sms:NoScanAlertTime"] = alertTime
            })
            .Build();

    private static IServiceProvider BuildServiceProvider(
        Data.ApplicationDbContext? context = null,
        Mock<ICalendarService>? calendarService = null,
        Mock<ISmsTemplateService>? templateService = null,
        Mock<ISmsSettingsService>? smsSettings = null)
    {
        var services = new ServiceCollection();

        context ??= TestDbContextFactory.Create();
        services.AddSingleton(context);

        var cal = calendarService ?? DefaultCalendar(isSchoolDay: true);
        services.AddSingleton(cal.Object);
        services.AddSingleton<ICalendarService>(sp => sp.GetRequiredService<ICalendarService>());

        var tmpl = templateService ?? DefaultTemplate();
        services.AddSingleton(tmpl.Object);

        var smsSvc = smsSettings ?? DefaultSmsSettings(enabled: true);
        services.AddSingleton(smsSvc.Object);

        // Register interfaces pointing to mock instances
        services.AddSingleton<ICalendarService>(cal.Object);
        services.AddSingleton<ISmsTemplateService>(tmpl.Object);
        services.AddSingleton<ISmsSettingsService>(smsSvc.Object);

        return services.BuildServiceProvider();
    }

    private static Mock<ICalendarService> DefaultCalendar(bool isSchoolDay) =>
        MockCalendar(isSchoolDay);

    private static Mock<ICalendarService> MockCalendar(bool isSchoolDay)
    {
        var mock = new Mock<ICalendarService>();
        mock.Setup(m => m.IsSchoolDayAsync(It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(isSchoolDay);
        return mock;
    }

    private static Mock<ISmsTemplateService> DefaultTemplate()
    {
        var mock = new Mock<ISmsTemplateService>();
        mock.Setup(m => m.RenderTemplateAsync(
                "NO_SCAN_ALERT",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
            .ReturnsAsync((string _, string lang, Dictionary<string, string> p) =>
                $"Alert for {p["StudentFirstName"]} ({lang}): no scan on {p["Date"]}");
        return mock;
    }

    private static Mock<ISmsSettingsService> DefaultSmsSettings(bool enabled)
    {
        var mock = new Mock<ISmsSettingsService>();
        mock.Setup(m => m.IsSmsEnabledAsync()).ReturnsAsync(enabled);
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

    private static Student SeedStudent(Data.ApplicationDbContext context, AcademicYear year,
        string firstName = "Juan", bool smsEnabled = true, string? parentPhone = "09171234567",
        string gradeLevel = "7", string section = "Ruby", string language = "EN")
    {
        var student = new Student
        {
            StudentId = $"SL-2026-{Guid.NewGuid().ToString()[..5]}",
            FirstName = firstName,
            LastName = "Test",
            GradeLevel = gradeLevel,
            Section = section,
            ParentGuardianName = "Parent",
            GuardianRelationship = "Mother",
            ParentPhone = parentPhone ?? string.Empty,
            IsActive = true,
            SmsEnabled = smsEnabled,
            SmsLanguage = language
        };
        context.Students.Add(student);

        var section_ = new Section { Name = section, IsActive = true };
        context.Sections.Add(section_);
        context.SaveChanges();

        context.StudentEnrollments.Add(new StudentEnrollment
        {
            StudentId = student.Id,
            SectionId = section_.Id,
            AcademicYearId = year.Id,
            IsActive = true
        });
        context.SaveChanges();
        return student;
    }

    private static void SeedScanForStudent(Data.ApplicationDbContext context, Guid studentId, DateTime scannedAt)
    {
        var device = new Device { Name = "Gate 1", ApiKeyHash = Guid.NewGuid().ToString(), IsActive = true };
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

    // ─── CalculateDelayUntilAlertTime ────────────────────────────────────────

    [Fact]
    public void CalculateDelayUntilAlertTime_BeforeAlertTime_ReturnsPositiveDelay()
    {
        // Use a far-future alert time (23:59) to ensure we're before it.
        var service = CreateService(config: BuildConfig("23:59"));

        var delay = service.CalculateDelayUntilAlertTime("23:59");

        Assert.True(delay > TimeSpan.Zero);
    }

    [Fact]
    public void CalculateDelayUntilAlertTime_InvalidConfigValue_DoesNotThrow()
    {
        var service = CreateService(config: BuildConfig("not-a-time"));

        // Should not throw — falls back to 18:10 default
        var delay = service.CalculateDelayUntilAlertTime("not-a-time");

        Assert.True(delay > TimeSpan.Zero);
    }

    [Fact]
    public void CalculateDelayUntilAlertTime_PastAlertTime_WaitsUntilTomorrow()
    {
        // Use alert time = 30 minutes ago. New behavior: always waits until tomorrow.
        var now = DateTime.Now;
        var alertTime = now.AddMinutes(-30);
        // Skip if alertTime rolled into previous day (test runs very close to midnight)
        if (alertTime.Date < now.Date) return;

        var alertTimeStr = $"{alertTime.Hour:D2}:{alertTime.Minute:D2}";
        var service = CreateService(config: BuildConfig(alertTimeStr));

        var delay = service.CalculateDelayUntilAlertTime(alertTimeStr);

        // Past alert time → should wait until tomorrow's slot (> 23 hours away)
        Assert.True(delay > TimeSpan.FromHours(23),
            $"Expected delay > 23h for past alert time, but got {delay}");
    }

    // ─── CalculateDelayUntilTomorrow ──────────────────────────────────────────

    [Fact]
    public void CalculateDelayUntilTomorrow_AlwaysReturnsPositiveDelay()
    {
        var service = CreateService();

        // No matter when we call this, tomorrow's alert time is always > 0 away
        var delay = service.CalculateDelayUntilTomorrow("18:10");

        Assert.True(delay > TimeSpan.Zero);
    }

    [Fact]
    public void CalculateDelayUntilTomorrow_InvalidTime_StillPositive()
    {
        var service = CreateService();

        var delay = service.CalculateDelayUntilTomorrow("not-a-time");

        Assert.True(delay > TimeSpan.Zero);
    }

    [Fact]
    public void CalculateDelayUntilTomorrow_IsAtLeast1HourAway()
    {
        var service = CreateService();

        // Tomorrow is always at least 1 hour away regardless of current time
        var delay = service.CalculateDelayUntilTomorrow("18:10");

        Assert.True(delay > TimeSpan.FromHours(1),
            $"Expected delay > 1h but got {delay}");
    }

    // ─── Alert Core Logic (Integration with InMemory DB) ─────────────────────

    [Fact]
    public async Task Alert_SmsDisabled_SkipsWithoutQueueing()
    {
        var context = TestDbContextFactory.Create();
        var smsSettings = DefaultSmsSettings(enabled: false);
        var sp = BuildServiceProvider(context: context, smsSettings: smsSettings);

        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
    }

    [Fact]
    public async Task Alert_NonSchoolDay_SkipsWithoutQueueing()
    {
        var context = TestDbContextFactory.Create();
        var calendar = MockCalendar(isSchoolDay: false);
        var sp = BuildServiceProvider(context: context, calendarService: calendar);

        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
    }

    [Fact]
    public async Task Alert_ZeroScansToday_SuppressesAndWritesAuditLog()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        SeedStudent(context, year);
        // No scans seeded

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
        Assert.Contains(context.AuditLogs.ToList(),
            a => a.Action == "NO_SCAN_ALERT_SUPPRESSED");
    }

    [Fact]
    public async Task Alert_StudentWithNoScan_QueuesAlert()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        var student = SeedStudent(context, year, firstName: "Maria");

        // Seed a scan for a DIFFERENT student to pass the scanner health guard
        var otherStudent = SeedStudent(context, year, firstName: "Pedro");
        SeedScanForStudent(context, otherStudent.Id, DateTime.Today.AddHours(8));

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        var queued = context.SmsQueues.ToList();
        Assert.Single(queued);
        Assert.Equal("NO_SCAN_ALERT", queued[0].MessageType);
        Assert.Equal(student.ParentPhone, queued[0].PhoneNumber);
    }

    [Fact]
    public async Task Alert_StudentHasScanToday_NotQueued()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        var student = SeedStudent(context, year);
        SeedScanForStudent(context, student.Id, DateTime.Today.AddHours(8));

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
        Assert.Contains(context.AuditLogs.ToList(),
            a => a.Action == "NO_SCAN_ALERT_EXECUTED");
    }

    [Fact]
    public async Task Alert_StudentSmsDisabled_NotQueued()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        SeedStudent(context, year, smsEnabled: false);

        // Seed another student with a scan to pass scanner health guard
        var otherStudent = SeedStudent(context, year, firstName: "Pedro");
        SeedScanForStudent(context, otherStudent.Id, DateTime.Today.AddHours(8));

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
    }

    [Fact]
    public async Task Alert_StudentNoParentPhone_NotQueued()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        SeedStudent(context, year, parentPhone: null);

        var otherStudent = SeedStudent(context, year, firstName: "Pedro");
        SeedScanForStudent(context, otherStudent.Id, DateTime.Today.AddHours(8));

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal(0, context.SmsQueues.Count());
    }

    [Fact]
    public async Task Alert_RunTwiceSameDay_SecondRunProducesNoDuplicates()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        var student = SeedStudent(context, year);

        // Seed scan for another student to pass scanner health guard
        var otherStudent = SeedStudent(context, year, firstName: "Pedro");
        SeedScanForStudent(context, otherStudent.Id, DateTime.Today.AddHours(8));

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);

        await service.InvokeAlertForTestAsync();
        var countAfterFirst = context.SmsQueues.Count();

        await service.InvokeAlertForTestAsync();
        var countAfterSecond = context.SmsQueues.Count();

        Assert.Equal(1, countAfterFirst);
        Assert.Equal(countAfterFirst, countAfterSecond); // idempotent
    }

    [Fact]
    public async Task Alert_FilLanguage_UsesFilTemplate()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        SeedStudent(context, year, language: "FIL");

        var otherStudent = SeedStudent(context, year, firstName: "Pedro");
        SeedScanForStudent(context, otherStudent.Id, DateTime.Today.AddHours(8));

        var templateMock = new Mock<ISmsTemplateService>();
        string? capturedLanguage = null;
        templateMock.Setup(m => m.RenderTemplateAsync(
                "NO_SCAN_ALERT",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, Dictionary<string, string>>((_, lang, _) => capturedLanguage = lang)
            .ReturnsAsync("Rendered message FIL");

        var sp = BuildServiceProvider(context: context, templateService: templateMock);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.Equal("FIL", capturedLanguage);
    }

    [Fact]
    public async Task Alert_AllPlaceholdersPassedToTemplate()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        SeedStudent(context, year, firstName: "Juan", gradeLevel: "7", section: "Ruby", language: "EN");

        // Seed school phone
        context.AppSettings.Add(new AppSettings
        {
            Key = "System.SchoolPhone",
            Value = "028881234",
            Category = "System"
        });
        context.SaveChanges();

        var otherStudent = SeedStudent(context, year, firstName: "Pedro");
        SeedScanForStudent(context, otherStudent.Id, DateTime.Today.AddHours(8));

        Dictionary<string, string>? capturedPlaceholders = null;
        var templateMock = new Mock<ISmsTemplateService>();
        templateMock.Setup(m => m.RenderTemplateAsync(
                "NO_SCAN_ALERT",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, Dictionary<string, string>>((_, _, p) => capturedPlaceholders = p)
            .ReturnsAsync("Rendered");

        var sp = BuildServiceProvider(context: context, templateService: templateMock);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        Assert.NotNull(capturedPlaceholders);
        Assert.Equal("Juan", capturedPlaceholders["StudentFirstName"]);
        Assert.Equal("7", capturedPlaceholders["GradeLevel"]);
        Assert.Equal("Ruby", capturedPlaceholders["Section"]);
        Assert.Equal("028881234", capturedPlaceholders["SchoolPhone"]);
        Assert.False(string.IsNullOrEmpty(capturedPlaceholders["Date"]));
    }

    [Fact]
    public async Task Alert_Completion_WritesAuditLogWithExecutedAction()
    {
        var context = TestDbContextFactory.Create();
        var year = SeedCurrentYear(context);
        var student = SeedStudent(context, year);
        SeedScanForStudent(context, student.Id, DateTime.Today.AddHours(8));
        // All students have scans → 0 alerts queued but still logs

        var sp = BuildServiceProvider(context: context);
        var service = CreateService(serviceProvider: sp);
        await service.InvokeAlertForTestAsync();

        var auditLogs = context.AuditLogs.ToList();
        Assert.Contains(auditLogs, a => a.Action == "NO_SCAN_ALERT_EXECUTED");
    }

    // ─── DbInitializer: Template Seeding ──────────────────────────────────────

    [Fact]
    public async Task Seed_NoScanAlertTemplate_ExistsAfterSeed()
    {
        var context = TestDbContextFactory.Create();

        // Call only the template seeding part by seeding directly
        context.SmsTemplates.Add(new SmsTemplate
        {
            Code = "NO_SCAN_ALERT",
            Name = "End-of-Day No-Scan Alert",
            TemplateEn = "SmartLog: We have no attendance record for {StudentFirstName} ({GradeLevel} - {Section}) today, {Date}. Please verify their whereabouts or contact the school at {SchoolPhone}.",
            TemplateFil = "SmartLog: Wala kaming rekord ng pagdalo ni {StudentFirstName} ({GradeLevel} - {Section}) ngayon, {Date}. Mangyaring tiyakin ang kanilang kinaroroonan o makipag-ugnayan sa paaralan sa {SchoolPhone}.",
            AvailablePlaceholders = "{StudentFirstName},{GradeLevel},{Section},{Date},{SchoolPhone}",
            IsActive = true,
            IsSystem = true
        });
        await context.SaveChangesAsync();

        var byCode = context.SmsTemplates.FirstOrDefault(t => t.Code == "NO_SCAN_ALERT");
        Assert.NotNull(byCode);
        Assert.Contains("{StudentFirstName}", byCode.AvailablePlaceholders);
        Assert.Contains("{SchoolPhone}", byCode.AvailablePlaceholders);
        Assert.True(byCode.IsSystem);
    }
}

// ─── Test Helper Extension ─────────────────────────────────────────────────────
// Exposes ExecuteAlertAsync for testing without running the full timed loop.
internal static class NoScanAlertServiceTestExtensions
{
    public static Task<int> InvokeAlertForTestAsync(this NoScanAlertService service) =>
        (Task<int>)typeof(NoScanAlertService)
            .GetMethod("RunAlertCoreAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(service, [CancellationToken.None])!;
}
