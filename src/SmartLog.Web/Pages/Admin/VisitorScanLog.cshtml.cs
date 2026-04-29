using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Visitor scan log page with entry/exit pairing and duration.
/// Implements US0075 (Visitor Scan Log).
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class VisitorScanLogModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;
    private readonly ApplicationDbContext _context;
    private readonly ITimezoneService _timezoneService;

    public VisitorScanLogModel(IVisitorPassService visitorPassService, ApplicationDbContext context, ITimezoneService timezoneService)
    {
        _visitorPassService = visitorPassService;
        _context = context;
        _timezoneService = timezoneService;
    }

    public DateTime TodayPht { get; private set; }

    [BindProperty(SupportsGet = true)]
    public string? DeviceFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? CameraFilter { get; set; }

    public List<DeviceInfo> Devices { get; set; } = new();
    public List<CameraInfo> AvailableCameras { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalPages { get; set; }
    public int TotalVisits { get; set; }

    public List<VisitorVisit> Visits { get; set; } = new();
    public VisitorLogSummary Summary { get; set; } = new();

    public async Task OnGetAsync()
    {
        TodayPht = _timezoneService.GetCurrentPhilippinesTime().Date;

        // Default to today in Philippines Time if no dates set
        StartDate ??= TodayPht;
        EndDate ??= TodayPht;

        if (PageNumber < 1) PageNumber = 1;

        // Load devices for filter dropdown
        Devices = await _context.Devices
            .Select(d => new DeviceInfo { Id = d.Id, Name = d.Name })
            .OrderBy(d => d.Name)
            .ToListAsync();

        // Load distinct camera combos scoped to selected device
        var deviceGuid = Guid.TryParse(DeviceFilter, out var parsedDevId) ? parsedDevId : (Guid?)null;
        var knownCameras = await _context.VisitorScans
            .Where(s => deviceGuid == null || s.DeviceId == deviceGuid)
            .Where(s => s.CameraIndex != null)
            .Select(s => new { s.CameraIndex, s.CameraName })
            .Distinct()
            .OrderBy(x => x.CameraIndex).ThenBy(x => x.CameraName)
            .ToListAsync();
        var hasLegacyRows = await _context.VisitorScans
            .Where(s => deviceGuid == null || s.DeviceId == deviceGuid)
            .AnyAsync(s => s.CameraIndex == null);
        AvailableCameras = knownCameras
            .Select(x => new CameraInfo { Index = x.CameraIndex, Name = x.CameraName })
            .ToList();
        if (hasLegacyRows)
            AvailableCameras.Insert(0, new CameraInfo { Index = null, Name = null });

        var result = await _visitorPassService.GetVisitorLogAsync(
            StartDate, EndDate, SearchTerm, PageNumber, PageSize, DeviceFilter, CameraFilter);

        Visits = result.Visits;
        TotalVisits = result.TotalCount;
        TotalPages = (int)Math.Ceiling(TotalVisits / (double)PageSize);
        Summary = result.Summary;

        // Convert UTC timestamps to Philippines Time for display
        foreach (var visit in Visits)
        {
            visit.EntryTime = _timezoneService.ToPhilippinesTime(visit.EntryTime);
            if (visit.ExitTime.HasValue)
                visit.ExitTime = _timezoneService.ToPhilippinesTime(visit.ExitTime.Value);
        }

        if (PageNumber > TotalPages && TotalPages > 0)
            PageNumber = TotalPages;
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
