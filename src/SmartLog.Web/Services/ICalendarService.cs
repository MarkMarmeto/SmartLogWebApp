using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for managing school calendar events, holidays, and class suspensions.
/// </summary>
public interface ICalendarService
{
    // Query methods
    /// <summary>
    /// Gets all events for a specific month.
    /// </summary>
    Task<List<CalendarEvent>> GetEventsForMonthAsync(int year, int month, Guid? academicYearId = null);

    /// <summary>
    /// Gets all events within a date range.
    /// </summary>
    Task<List<CalendarEvent>> GetEventsForDateRangeAsync(DateTime start, DateTime end, Guid? academicYearId = null);

    /// <summary>
    /// Gets upcoming events (next N events from today).
    /// </summary>
    Task<List<CalendarEvent>> GetUpcomingEventsAsync(int count = 5, Guid? academicYearId = null);

    /// <summary>
    /// Gets a specific event by ID.
    /// </summary>
    Task<CalendarEvent?> GetEventByIdAsync(Guid id);

    // Date checking methods
    /// <summary>
    /// Checks if a given date is a school day (not a holiday or suspension).
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <param name="gradeLevel">Optional grade level to check for grade-specific suspensions.</param>
    Task<bool> IsSchoolDayAsync(DateTime date, string? gradeLevel = null);

    /// <summary>
    /// Checks if a given date is a holiday.
    /// </summary>
    Task<bool> IsHolidayAsync(DateTime date);

    /// <summary>
    /// Gets all events for a specific date.
    /// </summary>
    Task<List<CalendarEvent>> GetEventsForDateAsync(DateTime date);

    // CRUD operations
    /// <summary>
    /// Creates a new calendar event.
    /// </summary>
    Task<CalendarEvent> CreateEventAsync(CalendarEvent calendarEvent);

    /// <summary>
    /// Updates an existing calendar event.
    /// </summary>
    Task<CalendarEvent> UpdateEventAsync(CalendarEvent calendarEvent);

    /// <summary>
    /// Deletes a calendar event by ID.
    /// </summary>
    Task DeleteEventAsync(Guid id);

    // Attendance integration methods
    /// <summary>
    /// Gets the count of school days within a date range (excludes holidays and suspensions).
    /// </summary>
    /// <param name="startDate">Start date of the range.</param>
    /// <param name="endDate">End date of the range.</param>
    /// <param name="gradeLevel">Optional grade level for grade-specific suspensions.</param>
    Task<int> GetSchoolDaysCountAsync(DateTime startDate, DateTime endDate, string? gradeLevel = null);

    /// <summary>
    /// Gets a list of all school days within a date range.
    /// </summary>
    Task<List<DateTime>> GetSchoolDaysAsync(DateTime startDate, DateTime endDate, string? gradeLevel = null);

    // Reporting
    /// <summary>
    /// Gets statistics about events for an academic year (holiday count, event count, etc.).
    /// </summary>
    Task<Dictionary<string, int>> GetEventStatisticsAsync(Guid academicYearId);
}
