using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class ScanLogsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public ScanLogsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<ScanLogEntry> Scans { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ScanTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StudentSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DeviceFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalPages { get; set; }
    public int TotalScans { get; set; }

    public int AcceptedCount { get; set; }
    public int RejectedCount { get; set; }
    public int DuplicateCount { get; set; }

    public List<DeviceInfo> Devices { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Load devices for filter dropdown
        Devices = await _context.Devices
            .Select(d => new DeviceInfo { Id = d.Id, Name = d.Name })
            .OrderBy(d => d.Name)
            .ToListAsync();

        var query = _context.Scans
            .Include(s => s.Student)
            .Include(s => s.Device)
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            query = query.Where(s => s.Status == StatusFilter);
        }

        if (!string.IsNullOrWhiteSpace(ScanTypeFilter))
        {
            query = query.Where(s => s.ScanType == ScanTypeFilter);
        }

        if (!string.IsNullOrWhiteSpace(StudentSearch))
        {
            query = query.Where(s =>
                s.Student.FirstName.Contains(StudentSearch) ||
                s.Student.LastName.Contains(StudentSearch) ||
                s.Student.StudentId.Contains(StudentSearch));
        }

        if (!string.IsNullOrWhiteSpace(DeviceFilter) && Guid.TryParse(DeviceFilter, out var deviceId))
        {
            query = query.Where(s => s.DeviceId == deviceId);
        }

        if (StartDate.HasValue)
        {
            query = query.Where(s => s.ScannedAt >= StartDate.Value);
        }

        if (EndDate.HasValue)
        {
            var endOfDay = EndDate.Value.AddDays(1);
            query = query.Where(s => s.ScannedAt < endOfDay);
        }

        // Get summary counts (from filtered set)
        AcceptedCount = await query.CountAsync(s => s.Status == "ACCEPTED");
        RejectedCount = await query.CountAsync(s => s.Status.StartsWith("REJECTED"));
        DuplicateCount = await query.CountAsync(s => s.Status == "DUPLICATE");

        // Get total count for pagination
        TotalScans = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalScans / (double)PageSize);

        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        // Get paginated results
        Scans = await query
            .OrderByDescending(s => s.ScannedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(s => new ScanLogEntry
            {
                Id = s.Id,
                ScannedAt = s.ScannedAt,
                ReceivedAt = s.ReceivedAt,
                ScanType = s.ScanType,
                Status = s.Status,
                StudentId = s.Student.StudentId,
                StudentName = s.Student.FirstName + " " + s.Student.LastName,
                GradeLevel = s.Student.GradeLevel,
                Section = s.Student.Section,
                DeviceName = s.Device.Name
            })
            .ToListAsync();
    }

    public class ScanLogEntry
    {
        public Guid Id { get; set; }
        public DateTime ScannedAt { get; set; }
        public DateTime ReceivedAt { get; set; }
        public string ScanType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string GradeLevel { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
    }

    public class DeviceInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
