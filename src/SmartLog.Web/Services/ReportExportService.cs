using System.Net;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using System.Text;

namespace SmartLog.Web.Services;

/// <summary>
/// Service for exporting reports to various formats.
/// Implements US0049 (Report Export).
/// Note: This implementation uses CSV format for Excel compatibility and HTML for PDF printing.
/// For production, consider using libraries like ClosedXML for Excel and QuestPDF for PDF.
/// </summary>
public class ReportExportService : IReportExportService
{
    private readonly ApplicationDbContext _context;
    private readonly IAttendanceService _attendanceService;
    private readonly ITimezoneService _timezoneService;

    public ReportExportService(
        ApplicationDbContext context,
        IAttendanceService attendanceService,
        ITimezoneService timezoneService)
    {
        _context = context;
        _attendanceService = attendanceService;
        _timezoneService = timezoneService;
    }

    public async Task<byte[]> ExportDailyAttendanceToExcelAsync(DateTime date, string? grade, string? section)
    {
        // US0049-AC1: Export daily attendance to CSV (Excel-compatible)
        var summary = await _attendanceService.GetAttendanceSummaryAsync(date, grade, section);
        var records = await _attendanceService.GetAttendanceListAsync(date, grade, section, null, null, 1, 10000);

        var csv = new StringBuilder();
        csv.AppendLine("SmartLog - Daily Attendance Report");
        csv.AppendLine($"Date:,{date:MMMM dd, yyyy}");
        if (!string.IsNullOrEmpty(grade)) csv.AppendLine($"Grade:,{grade}");
        if (!string.IsNullOrEmpty(section)) csv.AppendLine($"Section:,{section}");
        csv.AppendLine($"Generated:,{_timezoneService.FormatForDisplay(DateTime.UtcNow)}");
        csv.AppendLine();

        csv.AppendLine("Summary Statistics");
        csv.AppendLine($"Total Enrolled:,{summary.TotalEnrolled}");
        csv.AppendLine($"Present:,{summary.Present}");
        csv.AppendLine($"Absent:,{summary.Absent}");
        csv.AppendLine($"Departed:,{summary.Departed}");
        csv.AppendLine($"Attendance Rate:,{summary.AttendanceRate}%");
        csv.AppendLine();

        csv.AppendLine("Student ID,Name,Grade,Section,Status,Entry Time,Exit Time");
        foreach (var record in records.OrderBy(r => int.TryParse(r.GradeLevel, out var g) ? g : 0).ThenBy(r => r.Section).ThenBy(r => r.FullName))
        {
            csv.AppendLine($"{CsvEscape(record.StudentIdNumber)},{CsvEscape(record.FullName)},{CsvEscape(record.GradeLevel)},{CsvEscape(record.Section)}," +
                          $"{record.Status},{FormatPht(record.EntryTime, "HH:mm")},{FormatPht(record.ExitTime, "HH:mm")}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportDailyAttendanceToPdfAsync(DateTime date, string? grade, string? section)
    {
        // US0049-AC2: Export to HTML for PDF printing
        var summary = await _attendanceService.GetAttendanceSummaryAsync(date, grade, section);
        var records = await _attendanceService.GetAttendanceListAsync(date, grade, section, null, null, 1, 10000);

        var html = GenerateDailyAttendanceHtml(date, grade, section, summary, records);
        return Encoding.UTF8.GetBytes(html);
    }

    public async Task<byte[]> ExportWeeklyAttendanceToExcelAsync(DateTime weekStart, string? grade, string? section)
    {
        var weekEnd = weekStart.AddDays(6);
        var csv = new StringBuilder();
        csv.AppendLine("SmartLog - Weekly Attendance Summary");
        csv.AppendLine($"Week:,{weekStart:MMM dd} - {weekEnd:MMM dd, yyyy}");
        if (!string.IsNullOrEmpty(grade)) csv.AppendLine($"Grade:,{grade}");
        if (!string.IsNullOrEmpty(section)) csv.AppendLine($"Section:,{section}");
        csv.AppendLine($"Generated:,{_timezoneService.FormatForDisplay(DateTime.UtcNow)}");
        csv.AppendLine();

        csv.AppendLine("Date,Day,Total Enrolled,Present,Departed,Absent,Attendance Rate %");

        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var summary = await _attendanceService.GetAttendanceSummaryAsync(date, grade, section);
            csv.AppendLine($"{date:MMM dd yyyy},{date:dddd},{summary.TotalEnrolled},{summary.Present}," +
                          $"{summary.Departed},{summary.Absent},{summary.AttendanceRate}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportWeeklyAttendanceToPdfAsync(DateTime weekStart, string? grade, string? section)
    {
        // Generate HTML for PDF printing
        var weekEnd = weekStart.AddDays(6);
        var html = new StringBuilder();
        html.AppendLine("<html><head><title>Weekly Attendance Summary</title>");
        html.AppendLine("<style>body{font-family:Arial;} table{border-collapse:collapse;width:100%;} " +
                       "th,td{border:1px solid #ddd;padding:8px;text-align:left;} th{background-color:#0d6efd;color:white;}</style></head><body>");
        html.AppendLine($"<h1>Weekly Attendance Summary</h1>");
        html.AppendLine($"<p><strong>Week:</strong> {weekStart:MMMM dd} - {weekEnd:MMMM dd, yyyy}</p>");
        if (!string.IsNullOrEmpty(grade)) html.AppendLine($"<p><strong>Grade:</strong> {HtmlEsc(grade)}</p>");
        if (!string.IsNullOrEmpty(section)) html.AppendLine($"<p><strong>Section:</strong> {HtmlEsc(section)}</p>");
        html.AppendLine($"<p><strong>Generated:</strong> {_timezoneService.FormatForDisplay(DateTime.UtcNow)}</p>");

        html.AppendLine("<table><tr><th>Date</th><th>Day</th><th>Total Enrolled</th><th>Present</th><th>Departed</th><th>Absent</th><th>Attendance Rate</th></tr>");

        for (int i = 0; i < 7; i++)
        {
            var date = weekStart.AddDays(i);
            var summary = await _attendanceService.GetAttendanceSummaryAsync(date, grade, section);
            html.AppendLine($"<tr><td>{date:MMM dd, yyyy}</td><td>{date:dddd}</td><td>{summary.TotalEnrolled}</td>" +
                          $"<td>{summary.Present}</td><td>{summary.Departed}</td><td>{summary.Absent}</td><td>{summary.AttendanceRate}%</td></tr>");
        }

        html.AppendLine("</table></body></html>");
        return Encoding.UTF8.GetBytes(html.ToString());
    }

    public async Task<byte[]> ExportMonthlyAttendanceToExcelAsync(int year, int month, string? grade, string? section)
    {
        var monthStart = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var csv = new StringBuilder();
        csv.AppendLine("SmartLog - Monthly Attendance Report");
        csv.AppendLine($"Month:,{monthStart:MMMM yyyy}");
        if (!string.IsNullOrEmpty(grade)) csv.AppendLine($"Grade:,{grade}");
        if (!string.IsNullOrEmpty(section)) csv.AppendLine($"Section:,{section}");
        csv.AppendLine($"Generated:,{_timezoneService.FormatForDisplay(DateTime.UtcNow)}");
        csv.AppendLine();

        csv.AppendLine("Date,Day,Total Enrolled,Present,Departed,Absent,Attendance Rate %");

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var summary = await _attendanceService.GetAttendanceSummaryAsync(date, grade, section);
            csv.AppendLine($"{date:MMM dd yyyy},{date:dddd},{summary.TotalEnrolled},{summary.Present}," +
                          $"{summary.Departed},{summary.Absent},{summary.AttendanceRate}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportMonthlyAttendanceToPdfAsync(int year, int month, string? grade, string? section)
    {
        var monthStart = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var html = new StringBuilder();
        html.AppendLine("<html><head><title>Monthly Attendance Report</title>");
        html.AppendLine("<style>body{font-family:Arial;} table{border-collapse:collapse;width:100%;} " +
                       "th,td{border:1px solid #ddd;padding:8px;text-align:left;} th{background-color:#0d6efd;color:white;}</style></head><body>");
        html.AppendLine($"<h1>Monthly Attendance Report</h1>");
        html.AppendLine($"<p><strong>Month:</strong> {monthStart:MMMM yyyy}</p>");
        if (!string.IsNullOrEmpty(grade)) html.AppendLine($"<p><strong>Grade:</strong> {HtmlEsc(grade)}</p>");
        if (!string.IsNullOrEmpty(section)) html.AppendLine($"<p><strong>Section:</strong> {HtmlEsc(section)}</p>");
        html.AppendLine($"<p><strong>Generated:</strong> {_timezoneService.FormatForDisplay(DateTime.UtcNow)}</p>");

        html.AppendLine("<table><tr><th>Date</th><th>Day</th><th>Total</th><th>Present</th><th>Departed</th><th>Absent</th><th>Rate</th></tr>");

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var summary = await _attendanceService.GetAttendanceSummaryAsync(date, grade, section);
            html.AppendLine($"<tr><td>{date:MMM dd}</td><td>{date:ddd}</td><td>{summary.TotalEnrolled}</td>" +
                          $"<td>{summary.Present}</td><td>{summary.Departed}</td><td>{summary.Absent}</td><td>{summary.AttendanceRate}%</td></tr>");
        }

        html.AppendLine("</table></body></html>");
        return Encoding.UTF8.GetBytes(html.ToString());
    }

    public async Task<byte[]> ExportStudentHistoryToExcelAsync(Guid studentId, DateTime startDate, DateTime endDate)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student == null) throw new InvalidOperationException("Student not found");

        var csv = new StringBuilder();
        csv.AppendLine("SmartLog - Student Attendance History");
        csv.AppendLine($"Student:,{CsvEscape($"{student.FirstName} {student.LastName}")}");
        csv.AppendLine($"Student ID:,{CsvEscape(student.StudentId)}");
        csv.AppendLine($"Grade:,{CsvEscape($"{student.GradeLevel}-{student.Section}")}");
        csv.AppendLine($"Period:,{startDate:MMM dd yyyy} - {endDate:MMM dd yyyy}");
        csv.AppendLine($"Generated:,{_timezoneService.FormatForDisplay(DateTime.UtcNow)}");
        csv.AppendLine();

        csv.AppendLine("Date,Day,Status,Entry Time,Exit Time");

        // Single query for entire date range
        var allScans = await _context.Scans
            .Where(s => s.StudentId == studentId && s.ScannedAt >= startDate.Date && s.ScannedAt < endDate.Date.AddDays(1) && s.Status == "ACCEPTED")
            .OrderBy(s => s.ScannedAt)
            .ToListAsync();

        var scansByDate = allScans.GroupBy(s => s.ScannedAt.Date).ToDictionary(g => g.Key, g => g.ToList());

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            scansByDate.TryGetValue(date, out var scans);

            var entryTime = scans?.FirstOrDefault(s => s.ScanType == "ENTRY")?.ScannedAt;
            var exitTime = scans?.FirstOrDefault(s => s.ScanType == "EXIT")?.ScannedAt;
            var status = entryTime.HasValue && exitTime.HasValue ? "Departed" :
                        entryTime.HasValue ? "Present" : "Absent";

            csv.AppendLine($"{date:MMM dd yyyy},{date:dddd},{status}," +
                          $"{FormatPht(entryTime, "HH:mm")},{FormatPht(exitTime, "HH:mm")}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    public async Task<byte[]> ExportStudentHistoryToPdfAsync(Guid studentId, DateTime startDate, DateTime endDate)
    {
        var student = await _context.Students.FindAsync(studentId);
        if (student == null) throw new InvalidOperationException("Student not found");

        var html = new StringBuilder();
        html.AppendLine("<html><head><title>Student Attendance History</title>");
        html.AppendLine("<style>body{font-family:Arial;} table{border-collapse:collapse;width:100%;} " +
                       "th,td{border:1px solid #ddd;padding:8px;text-align:left;} th{background-color:#0d6efd;color:white;}</style></head><body>");
        html.AppendLine($"<h1>Student Attendance History</h1>");
        html.AppendLine($"<p><strong>Student:</strong> {HtmlEsc($"{student.FirstName} {student.LastName}")} ({HtmlEsc(student.StudentId)})</p>");
        html.AppendLine($"<p><strong>Grade:</strong> {HtmlEsc(student.GradeLevel)} - Section {HtmlEsc(student.Section)}</p>");
        html.AppendLine($"<p><strong>Period:</strong> {startDate:MMMM dd, yyyy} - {endDate:MMMM dd, yyyy}</p>");
        html.AppendLine($"<p><strong>Generated:</strong> {_timezoneService.FormatForDisplay(DateTime.UtcNow)}</p>");

        html.AppendLine("<table><tr><th>Date</th><th>Day</th><th>Status</th><th>Entry Time</th><th>Exit Time</th></tr>");

        // Single query for entire date range
        var allScansHtml = await _context.Scans
            .Where(s => s.StudentId == studentId && s.ScannedAt >= startDate.Date && s.ScannedAt < endDate.Date.AddDays(1) && s.Status == "ACCEPTED")
            .OrderBy(s => s.ScannedAt)
            .ToListAsync();

        var scansByDateHtml = allScansHtml.GroupBy(s => s.ScannedAt.Date).ToDictionary(g => g.Key, g => g.ToList());

        for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
        {
            scansByDateHtml.TryGetValue(date, out var scans);

            var entryTime = scans?.FirstOrDefault(s => s.ScanType == "ENTRY")?.ScannedAt;
            var exitTime = scans?.FirstOrDefault(s => s.ScanType == "EXIT")?.ScannedAt;
            var status = entryTime.HasValue && exitTime.HasValue ? "Departed" :
                        entryTime.HasValue ? "Present" : "Absent";

            html.AppendLine($"<tr><td>{date:MMM dd, yyyy}</td><td>{date:dddd}</td><td>{HtmlEsc(status)}</td>" +
                          $"<td>{FormatPht(entryTime, "h:mm tt")}</td><td>{FormatPht(exitTime, "h:mm tt")}</td></tr>");
        }

        html.AppendLine("</table></body></html>");
        return Encoding.UTF8.GetBytes(html.ToString());
    }

    public async Task<byte[]> ExportAuditLogsToExcelAsync(DateTime? startDate, DateTime? endDate, string? action, string? userId, string? searchTerm)
    {
        var defaultStartDate = startDate ?? DateTime.UtcNow.AddDays(-1);
        var defaultEndDate = endDate ?? DateTime.UtcNow;

        var query = _context.AuditLogs
            .Where(a => a.Timestamp >= defaultStartDate && a.Timestamp <= defaultEndDate);

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.PerformedByUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var search = searchTerm.ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(search) ||
                (a.Details != null && a.Details.ToLower().Contains(search)));
        }

        var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("SmartLog - Audit Logs Export");
        csv.AppendLine($"Period:,{_timezoneService.FormatForDisplay(defaultStartDate, "yyyy-MM-dd hh:mm tt")} - {_timezoneService.FormatForDisplay(defaultEndDate, "yyyy-MM-dd hh:mm tt")}");
        csv.AppendLine($"Generated:,{_timezoneService.FormatForDisplay(DateTime.UtcNow)}");
        csv.AppendLine();

        csv.AppendLine("Timestamp,Action,User,Performed By,Details,IP Address");
        foreach (var log in logs)
        {
            csv.AppendLine($"{_timezoneService.FormatForDisplay(log.Timestamp)},{CsvEscape(log.Action)}," +
                          $"{CsvEscape(log.UserName ?? "-")},{CsvEscape(log.PerformedByUserName ?? "System")}," +
                          $"{CsvEscape(log.Details)},{CsvEscape(log.IpAddress ?? "-")}");
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private string GenerateDailyAttendanceHtml(DateTime date, string? grade, string? section,
        AttendanceSummary summary, List<StudentAttendanceRecord> records)
    {
        var html = new StringBuilder();
        html.AppendLine("<html><head><title>Daily Attendance Report</title>");
        html.AppendLine("<style>body{font-family:Arial;margin:20px;} table{border-collapse:collapse;width:100%;margin-top:20px;} " +
                       "th,td{border:1px solid #ddd;padding:8px;text-align:left;} th{background-color:#0d6efd;color:white;} " +
                       ".summary{background-color:#f8f9fa;padding:15px;border-radius:5px;margin:20px 0;}</style></head><body>");

        html.AppendLine($"<h1>Daily Attendance Report</h1>");
        html.AppendLine($"<p><strong>Date:</strong> {date:MMMM dd, yyyy (dddd)}</p>");
        if (!string.IsNullOrEmpty(grade)) html.AppendLine($"<p><strong>Grade:</strong> {HtmlEsc(grade)}</p>");
        if (!string.IsNullOrEmpty(section)) html.AppendLine($"<p><strong>Section:</strong> {HtmlEsc(section)}</p>");
        html.AppendLine($"<p><strong>Generated:</strong> {_timezoneService.FormatForDisplay(DateTime.UtcNow)}</p>");

        html.AppendLine("<div class='summary'>");
        html.AppendLine($"<h3>Summary</h3>");
        html.AppendLine($"<p><strong>Total Enrolled:</strong> {summary.TotalEnrolled} | ");
        html.AppendLine($"<strong>Present:</strong> {summary.Present} | ");
        html.AppendLine($"<strong>Absent:</strong> {summary.Absent} | ");
        html.AppendLine($"<strong>Departed:</strong> {summary.Departed} | ");
        html.AppendLine($"<strong>Attendance Rate:</strong> {summary.AttendanceRate}%</p>");
        html.AppendLine("</div>");

        html.AppendLine("<table><tr><th>Student ID</th><th>Name</th><th>Grade</th><th>Section</th><th>Status</th><th>Entry</th><th>Exit</th></tr>");
        foreach (var record in records.OrderBy(r => int.TryParse(r.GradeLevel, out var g) ? g : 0).ThenBy(r => r.Section).ThenBy(r => r.FullName))
        {
            html.AppendLine($"<tr><td>{HtmlEsc(record.StudentIdNumber)}</td><td>{HtmlEsc(record.FullName)}</td><td>{HtmlEsc(record.GradeLevel)}</td>" +
                          $"<td>{HtmlEsc(record.Section)}</td><td>{HtmlEsc(record.Status)}</td>" +
                          $"<td>{FormatPht(record.EntryTime, "h:mm tt")}</td>" +
                          $"<td>{FormatPht(record.ExitTime, "h:mm tt")}</td></tr>");
        }
        html.AppendLine("</table></body></html>");

        return html.ToString();
    }

    /// <summary>
    /// Escapes a value for safe CSV output. Prevents formula injection (=, +, -, @, tab, CR)
    /// and handles commas/quotes/newlines per RFC 4180.
    /// </summary>
    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";

        // Prefix formula-triggering characters to prevent CSV injection in Excel/Calc
        if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n')
            value = "'" + value;

        // Quote the field if it contains special CSV characters
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";

        return value;
    }

    /// <summary>
    /// HTML-encodes a value for safe inclusion in HTML exports.
    /// </summary>
    private static string HtmlEsc(string? value)
    {
        return WebUtility.HtmlEncode(value ?? "");
    }

    /// <summary>
    /// Formats a UTC DateTime to Philippines time with the given format. Returns fallback if null.
    /// </summary>
    private string FormatPht(DateTime? utcDateTime, string format, string fallback = "-")
    {
        if (!utcDateTime.HasValue) return fallback;
        return _timezoneService.FormatForDisplay(utcDateTime.Value, format);
    }
}
