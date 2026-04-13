using Microsoft.Extensions.Logging;
using Moq;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Tests.Helpers;

namespace SmartLog.Web.Tests.Services;

public class CalendarServiceTests
{
    private readonly Mock<IAcademicYearService> _academicYearService = new();
    private readonly Mock<ILogger<CalendarService>> _logger = new();

    private CalendarService CreateService(Data.ApplicationDbContext? context = null)
    {
        context ??= TestDbContextFactory.Create();
        return new CalendarService(context, _academicYearService.Object, _logger.Object);
    }

    private static CalendarEvent CreateHoliday(Guid academicYearId, DateTime date, string title = "Holiday")
    {
        return new CalendarEvent
        {
            Title = title,
            EventType = EventType.Holiday,
            Category = "Holiday",
            StartDate = date,
            EndDate = date,
            AffectsAttendance = true,
            AcademicYearId = academicYearId,
            CreatedBy = "admin",
            IsActive = true
        };
    }

    private static CalendarEvent CreateSuspension(Guid academicYearId, DateTime date, string? affectedGrades = null)
    {
        return new CalendarEvent
        {
            Title = "Class Suspension",
            EventType = EventType.Suspension,
            Category = "Suspension",
            StartDate = date,
            EndDate = date,
            AffectsAttendance = true,
            AffectedGrades = affectedGrades,
            AcademicYearId = academicYearId,
            CreatedBy = "admin",
            IsActive = true
        };
    }

    // ========== IsSchoolDayAsync Tests ==========

    [Fact]
    public async Task IsSchoolDayAsync_Weekday_NoEvents_ReturnsTrue()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Monday April 13, 2026
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 13));
        Assert.True(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_Saturday_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Saturday April 11, 2026
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 11));
        Assert.False(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_Sunday_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        // Sunday April 12, 2026
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 12));
        Assert.False(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_Holiday_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        // Monday April 13, 2026 as holiday
        var holiday = CreateHoliday(ay.Id, new DateTime(2026, 4, 13), "Araw ng Kagitingan");
        context.CalendarEvents.Add(holiday);
        context.SaveChanges();

        var service = CreateService(context);
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 13));

        Assert.False(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_AllGradeSuspension_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        // null AffectedGrades = all grades
        context.CalendarEvents.Add(CreateSuspension(ay.Id, new DateTime(2026, 4, 13)));
        context.SaveChanges();

        var service = CreateService(context);
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 13), "7");

        Assert.False(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_GradeSpecificSuspension_AffectedGrade_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        context.CalendarEvents.Add(CreateSuspension(ay.Id, new DateTime(2026, 4, 13), "[\"7\",\"8\"]"));
        context.SaveChanges();

        var service = CreateService(context);
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 13), "7");

        Assert.False(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_GradeSpecificSuspension_UnaffectedGrade_ReturnsTrue()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        context.CalendarEvents.Add(CreateSuspension(ay.Id, new DateTime(2026, 4, 13), "[\"7\",\"8\"]"));
        context.SaveChanges();

        var service = CreateService(context);
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 13), "9");

        Assert.True(result);
    }

    [Fact]
    public async Task IsSchoolDayAsync_MalformedAffectedGrades_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        // Corrupted JSON — fail-safe should treat as all-grade suspension
        context.CalendarEvents.Add(CreateSuspension(ay.Id, new DateTime(2026, 4, 13), "NOT_VALID_JSON"));
        context.SaveChanges();

        var service = CreateService(context);
        var result = await service.IsSchoolDayAsync(new DateTime(2026, 4, 13), "7");

        Assert.False(result);
    }

    // ========== CreateEventAsync Tests ==========

    [Fact]
    public async Task CreateEventAsync_ValidEvent_Persists()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        var service = CreateService(context);
        var evt = CreateHoliday(ay.Id, new DateTime(2026, 6, 12), "Independence Day");

        var result = await service.CreateEventAsync(evt);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.IsActive);
        Assert.Single(context.CalendarEvents);
    }

    [Fact]
    public async Task CreateEventAsync_EndBeforeStart_Throws()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        var service = CreateService(context);
        var evt = new CalendarEvent
        {
            Title = "Bad Event",
            EventType = EventType.Event,
            Category = "Event",
            StartDate = new DateTime(2026, 6, 15),
            EndDate = new DateTime(2026, 6, 10), // before start
            AcademicYearId = ay.Id,
            CreatedBy = "admin"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateEventAsync(evt));
    }

    // ========== IsHolidayAsync Tests ==========

    [Fact]
    public async Task IsHolidayAsync_Holiday_ReturnsTrue()
    {
        using var context = TestDbContextFactory.Create();
        TestDbContextFactory.SeedAcademicYears(context);
        var ay = context.AcademicYears.First();

        context.CalendarEvents.Add(CreateHoliday(ay.Id, new DateTime(2026, 4, 13)));
        context.SaveChanges();

        var service = CreateService(context);
        var result = await service.IsHolidayAsync(new DateTime(2026, 4, 13));

        Assert.True(result);
    }

    [Fact]
    public async Task IsHolidayAsync_NoHoliday_ReturnsFalse()
    {
        using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        var result = await service.IsHolidayAsync(new DateTime(2026, 4, 13));
        Assert.False(result);
    }
}
