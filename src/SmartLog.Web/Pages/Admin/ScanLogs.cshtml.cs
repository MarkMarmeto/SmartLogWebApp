using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class ScanLogsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ITimezoneService _timezoneService;

    public ScanLogsModel(ApplicationDbContext context, ITimezoneService timezoneService)
    {
        _context = context;
        _timezoneService = timezoneService;
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
    public string? CameraFilter { get; set; }

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
    public List<CameraInfo> AvailableCameras { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Load devices for filter dropdown
        Devices = await _context.Devices
            .Select(d => new DeviceInfo { Id = d.Id, Name = d.Name })
            .OrderBy(d => d.Name)
            .ToListAsync();

        // Load distinct camera combos scoped to selected device
        var deviceGuid = Guid.TryParse(DeviceFilter, out var parsedDevId) ? parsedDevId : (Guid?)null;
        var knownCameras = await _context.Scans
            .Where(s => deviceGuid == null || s.DeviceId == deviceGuid)
            .Where(s => s.CameraIndex != null)
            .Select(s => new { s.CameraIndex, s.CameraName })
            .Distinct()
            .OrderBy(x => x.CameraIndex).ThenBy(x => x.CameraName)
            .ToListAsync();
        var hasLegacyRows = await _context.Scans
            .Where(s => deviceGuid == null || s.DeviceId == deviceGuid)
            .AnyAsync(s => s.CameraIndex == null);
        AvailableCameras = knownCameras
            .Select(x => new CameraInfo { Index = x.CameraIndex, Name = x.CameraName })
            .ToList();
        if (hasLegacyRows)
            AvailableCameras.Insert(0, new CameraInfo { Index = null, Name = null });

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

        if (!string.IsNullOrWhiteSpace(CameraFilter))
        {
            if (CameraFilter == "unknown")
            {
                query = query.Where(s => s.CameraIndex == null);
            }
            else
            {
                var parts = CameraFilter.Split('|', 2);
                if (int.TryParse(parts[0], out var idx))
                {
                    var name = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                    query = query.Where(s => s.CameraIndex == idx && s.CameraName == name);
                }
            }
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

        // Get summary counts in a single query instead of 4 separate round-trips
        var statusCounts = await query
            .GroupBy(s => s.Status == "ACCEPTED" ? "ACCEPTED" :
                         s.Status == "DUPLICATE" ? "DUPLICATE" :
                         s.Status.StartsWith("REJECTED") ? "REJECTED" : "OTHER")
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        AcceptedCount = statusCounts.Where(x => x.Category == "ACCEPTED").Sum(x => x.Count);
        RejectedCount = statusCounts.Where(x => x.Category == "REJECTED").Sum(x => x.Count);
        DuplicateCount = statusCounts.Where(x => x.Category == "DUPLICATE").Sum(x => x.Count);
        TotalScans = statusCounts.Sum(x => x.Count);
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
                DeviceName = s.Device.Name,
                CameraIndex = s.CameraIndex,
                CameraName = s.CameraName
            })
            .ToListAsync();

        // Convert UTC timestamps to Philippines time for display
        foreach (var scan in Scans)
        {
            scan.ScannedAt = _timezoneService.ToPhilippinesTime(scan.ScannedAt);
            scan.ReceivedAt = _timezoneService.ToPhilippinesTime(scan.ReceivedAt);
        }
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
        public int? CameraIndex { get; set; }
        public string? CameraName { get; set; }
    }

    public class DeviceInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CameraInfo
    {
        public int? Index { get; set; }
        public string? Name { get; set; }
        public string FilterValue => Index.HasValue ? $"{Index}|{Name ?? ""}" : "unknown";
        public string DisplayText => Index.HasValue ? $"{Index} · {Name ?? "—"}" : "(unknown)";
    }
}
