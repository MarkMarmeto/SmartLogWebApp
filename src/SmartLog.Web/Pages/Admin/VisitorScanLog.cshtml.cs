using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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

    public VisitorScanLogModel(IVisitorPassService visitorPassService)
    {
        _visitorPassService = visitorPassService;
    }

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
        // Default to today if no dates set
        StartDate ??= DateTime.UtcNow.Date;
        EndDate ??= DateTime.UtcNow.Date;

        if (PageNumber < 1) PageNumber = 1;

        var result = await _visitorPassService.GetVisitorLogAsync(
            StartDate, EndDate, SearchTerm, PageNumber, PageSize);

        Visits = result.Visits;
        TotalVisits = result.TotalCount;
        TotalPages = (int)Math.Ceiling(TotalVisits / (double)PageSize);
        Summary = result.Summary;

        if (PageNumber > TotalPages && TotalPages > 0)
            PageNumber = TotalPages;
    }
}
