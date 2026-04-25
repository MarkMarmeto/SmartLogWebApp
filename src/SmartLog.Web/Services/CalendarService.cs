using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models.Sms;

namespace SmartLog.Web.Services;

/// <summary>
/// Implementation of calendar service for managing school events, holidays, and suspensions.
/// </summary>
public class CalendarService : ICalendarService
{
    private readonly ApplicationDbContext _context;
    private readonly IAcademicYearService _academicYearService;
    private readonly ILogger<CalendarService> _logger;

    public CalendarService(
        ApplicationDbContext context,
        IAcademicYearService academicYearService,
        ILogger<CalendarService> logger)
    {
        _context = context;
        _academicYearService = academicYearService;
        _logger = logger;
    }

    public async Task<List<CalendarEvent>> GetEventsForMonthAsync(int year, int month, Guid? academicYearId = null)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);

        return await GetEventsForDateRangeAsync(startDate, endDate, academicYearId);
    }

    public async Task<List<CalendarEvent>> GetEventsForDateRangeAsync(DateTime start, DateTime end, Guid? academicYearId = null)
    {
        var query = _context.CalendarEvents
            .Include(e => e.AcademicYear)
            .Include(e => e.Organizer)
            .Where(e => e.IsActive);

        // Filter by academic year if specified
        if (academicYearId.HasValue)
        {
            query = query.Where(e => e.AcademicYearId == academicYearId.Value);
        }

        // Filter by date range - events that overlap with the range
        query = query.Where(e =>
            (e.StartDate >= start && e.StartDate <= end) ||
            (e.EndDate >= start && e.EndDate <= end) ||
            (e.StartDate <= start && e.EndDate >= end));

        return await query.OrderBy(e => e.StartDate).ToListAsync();
    }

    public async Task<List<CalendarEvent>> GetUpcomingEventsAsync(int count = 5, Guid? academicYearId = null)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.CalendarEvents
            .Include(e => e.AcademicYear)
            .Include(e => e.Organizer)
            .Where(e => e.IsActive && e.StartDate >= today);

        if (academicYearId.HasValue)
        {
            query = query.Where(e => e.AcademicYearId == academicYearId.Value);
        }

        return await query
            .OrderBy(e => e.StartDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<CalendarEvent?> GetEventByIdAsync(Guid id)
    {
        return await _context.CalendarEvents
            .Include(e => e.AcademicYear)
            .Include(e => e.Organizer)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<bool> IsSchoolDayAsync(DateTime date, string? gradeLevel = null)
    {
        var events = await GetEventsForDateAsync(date);

        // Check for holidays (affects all)
        if (events.Any(e => e.EventType == EventType.Holiday && e.AffectsAttendance))
        {
            return false;
        }

        // Check for suspensions
        var suspensions = events.Where(e => e.EventType == EventType.Suspension && e.AffectsAttendance);

        foreach (var suspension in suspensions)
        {
            // If suspension affects all grades (AffectedGrades is null)
            if (string.IsNullOrEmpty(suspension.AffectedGrades))
            {
                return false;
            }

            // If suspension affects specific grades and gradeLevel is provided
            if (!string.IsNullOrEmpty(gradeLevel))
            {
                try
                {
                    var affectedGrades = JsonSerializer.Deserialize<List<string>>(suspension.AffectedGrades);
                    if (affectedGrades != null && affectedGrades.Contains(gradeLevel))
                    {
                        return false;
                    }
                }
                catch (JsonException ex)
                {
                    // Fail-safe: if AffectedGrades JSON is corrupted, treat suspension as affecting all grades
                    _logger.LogError(ex, "Malformed AffectedGrades JSON for event {EventId} — treating as all-grade suspension", suspension.Id);
                    return false;
                }
            }
        }

        // Check if it's a weekend (Saturday=6, Sunday=0)
        var dayOfWeek = (int)date.DayOfWeek;
        if (dayOfWeek == 0 || dayOfWeek == 6)
        {
            return false;
        }

        return true;
    }

    public async Task<bool> IsHolidayAsync(DateTime date)
    {
        var events = await GetEventsForDateAsync(date);
        return events.Any(e => e.EventType == EventType.Holiday);
    }

    public async Task<List<CalendarEvent>> GetEventsForDateAsync(DateTime date)
    {
        var dateOnly = date.Date;

        return await _context.CalendarEvents
            .Include(e => e.AcademicYear)
            .Include(e => e.Organizer)
            .Where(e =>
                e.IsActive &&
                e.StartDate.Date <= dateOnly &&
                e.EndDate.Date >= dateOnly)
            .ToListAsync();
    }

    public async Task<CalendarEvent> CreateEventAsync(CalendarEvent calendarEvent)
    {
        // Validate
        if (calendarEvent.EndDate < calendarEvent.StartDate)
        {
            throw new ArgumentException("End date must be after start date.");
        }

        // Set defaults
        calendarEvent.CreatedAt = DateTime.UtcNow;
        calendarEvent.UpdatedAt = DateTime.UtcNow;
        calendarEvent.IsActive = true;

        _context.CalendarEvents.Add(calendarEvent);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created calendar event: {Title} ({EventType}) for {StartDate} - {EndDate}",
            calendarEvent.Title, calendarEvent.EventType, calendarEvent.StartDate, calendarEvent.EndDate);

        return calendarEvent;
    }

    public async Task<CalendarEvent> UpdateEventAsync(CalendarEvent calendarEvent)
    {
        var existing = await _context.CalendarEvents.FindAsync(calendarEvent.Id);
        if (existing == null)
        {
            throw new ArgumentException($"Calendar event with ID {calendarEvent.Id} not found.");
        }

        // Validate
        if (calendarEvent.EndDate < calendarEvent.StartDate)
        {
            throw new ArgumentException("End date must be after start date.");
        }

        // Update properties
        existing.Title = calendarEvent.Title;
        existing.Description = calendarEvent.Description;
        existing.EventType = calendarEvent.EventType;
        existing.Category = calendarEvent.Category;
        existing.StartDate = calendarEvent.StartDate;
        existing.EndDate = calendarEvent.EndDate;
        existing.IsAllDay = calendarEvent.IsAllDay;
        existing.StartTime = calendarEvent.StartTime;
        existing.EndTime = calendarEvent.EndTime;
        existing.AffectsAttendance = calendarEvent.AffectsAttendance;
        existing.AffectsClasses = calendarEvent.AffectsClasses;
        existing.AffectedGrades = calendarEvent.AffectedGrades;
        existing.SuppressesNoScanAlert = calendarEvent.SuppressesNoScanAlert;
        existing.Location = calendarEvent.Location;
        existing.IsRecurring = calendarEvent.IsRecurring;
        existing.RecurrencePattern = calendarEvent.RecurrencePattern;
        existing.RecurrenceEndDate = calendarEvent.RecurrenceEndDate;
        existing.AcademicYearId = calendarEvent.AcademicYearId;
        existing.OrganizerId = calendarEvent.OrganizerId;
        existing.UpdatedBy = calendarEvent.UpdatedBy;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.IsActive = calendarEvent.IsActive;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Updated calendar event: {Title} (ID: {EventId})",
            calendarEvent.Title, calendarEvent.Id);

        return existing;
    }

    public async Task DeleteEventAsync(Guid id)
    {
        var calendarEvent = await _context.CalendarEvents.FindAsync(id);
        if (calendarEvent == null)
        {
            throw new ArgumentException($"Calendar event with ID {id} not found.");
        }

        // Soft delete
        calendarEvent.IsActive = false;
        calendarEvent.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Deleted calendar event: {Title} (ID: {EventId})",
            calendarEvent.Title, id);
    }

    public async Task<int> GetSchoolDaysCountAsync(DateTime startDate, DateTime endDate, string? gradeLevel = null)
    {
        var schoolDays = await GetSchoolDaysAsync(startDate, endDate, gradeLevel);
        return schoolDays.Count;
    }

    public async Task<List<DateTime>> GetSchoolDaysAsync(DateTime startDate, DateTime endDate, string? gradeLevel = null)
    {
        var schoolDays = new List<DateTime>();
        var currentDate = startDate.Date;

        while (currentDate <= endDate.Date)
        {
            if (await IsSchoolDayAsync(currentDate, gradeLevel))
            {
                schoolDays.Add(currentDate);
            }

            currentDate = currentDate.AddDays(1);
        }

        return schoolDays;
    }

    public async Task<List<AlertSuppression>> GetTodaysSuppressionsAsync(DateOnly today)
    {
        var dateTime = today.ToDateTime(TimeOnly.MinValue);

        var events = await _context.CalendarEvents
            .Where(e =>
                e.IsActive &&
                e.StartDate.Date <= dateTime &&
                e.EndDate.Date >= dateTime &&
                (e.EventType == EventType.Holiday ||
                 e.EventType == EventType.Suspension ||
                 (e.EventType == EventType.Event && e.SuppressesNoScanAlert == true)))
            .ToListAsync();

        var suppressions = new List<AlertSuppression>();
        foreach (var ev in events)
        {
            var grades = new List<string>();
            if (!string.IsNullOrWhiteSpace(ev.AffectedGrades))
            {
                try
                {
                    grades = JsonSerializer.Deserialize<List<string>>(ev.AffectedGrades) ?? new List<string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Malformed AffectedGrades JSON for event {EventId} — treating as all-grade suppression", ev.Id);
                }
            }
            suppressions.Add(new AlertSuppression { Reason = ev.Title, GradeLevels = grades });
        }

        return suppressions;
    }

    public async Task<Dictionary<string, int>> GetEventStatisticsAsync(Guid academicYearId)
    {
        var events = await _context.CalendarEvents
            .Where(e => e.AcademicYearId == academicYearId && e.IsActive)
            .ToListAsync();

        var stats = new Dictionary<string, int>
        {
            { "TotalEvents", events.Count },
            { "Holidays", events.Count(e => e.EventType == EventType.Holiday) },
            { "SchoolEvents", events.Count(e => e.EventType == EventType.Event) },
            { "Suspensions", events.Count(e => e.EventType == EventType.Suspension) },
            { "AffectsAttendance", events.Count(e => e.AffectsAttendance) },
            { "AffectsClasses", events.Count(e => e.AffectsClasses) }
        };

        return stats;
    }
}
