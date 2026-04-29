using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Visitor pass management page.
/// Implements US0074 (Visitor Pass Admin Management).
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class VisitorPassesModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly ITimezoneService _timezoneService;

    public VisitorPassesModel(
        IVisitorPassService visitorPassService,
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        ITimezoneService timezoneService)
    {
        _visitorPassService = visitorPassService;
        _context = context;
        _userManager = userManager;
        _auditService = auditService;
        _timezoneService = timezoneService;
    }

    public List<VisitorPassViewModel> Passes { get; set; } = new();
    public int MaxPasses { get; set; }
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public int InUseCount { get; set; }
    public int DeactivatedCount { get; set; }
    public int AvailableToGenerate { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostGenerateAsync()
    {
        var passes = await _visitorPassService.GeneratePassesAsync();

        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "VisitorPassesGenerated",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Generated visitor passes (total: {passes.Count})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Visitor passes generated successfully. Total: {passes.Count}";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeactivateAsync(Guid id)
    {
        var pass = await _context.VisitorPasses.FindAsync(id);
        if (pass == null) return NotFound();

        await _visitorPassService.DeactivatePassAsync(id);

        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "VisitorPassDeactivated",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Deactivated visitor pass: {pass.Code}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Pass '{pass.Code}' has been deactivated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostActivateAsync(Guid id)
    {
        var pass = await _context.VisitorPasses.FindAsync(id);
        if (pass == null) return NotFound();

        await _visitorPassService.ActivatePassAsync(id);

        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "VisitorPassActivated",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Activated visitor pass: {pass.Code}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Pass '{pass.Code}' has been activated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSettingsAsync(int maxPasses)
    {
        if (maxPasses < 1)
        {
            ErrorMessage = "Maximum passes must be at least 1.";
            return RedirectToPage();
        }

        await _visitorPassService.SetMaxPassesAsync(maxPasses);
        await _visitorPassService.SyncPassCountAsync();

        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "VisitorMaxPassesUpdated",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Updated visitor max passes to {maxPasses}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Maximum passes updated to {maxPasses}.";
        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        MaxPasses = await _visitorPassService.GetMaxPassesAsync();
        var passes = await _visitorPassService.GetAllAsync();
        TotalCount = passes.Count;
        AvailableCount = passes.Count(p => p.IsActive && p.CurrentStatus == "Available");
        InUseCount = passes.Count(p => p.IsActive && p.CurrentStatus == "InUse");
        DeactivatedCount = passes.Count(p => !p.IsActive || p.CurrentStatus == "Deactivated");
        AvailableToGenerate = Math.Max(0, MaxPasses - TotalCount);

        // Load last entry/exit scan times for each pass
        var passIds = passes.Select(p => p.Id).ToList();
        var lastEntries = await _context.VisitorScans
            .Where(s => passIds.Contains(s.VisitorPassId) && s.ScanType == "ENTRY" && s.Status == "ACCEPTED")
            .GroupBy(s => s.VisitorPassId)
            .Select(g => new { PassId = g.Key, LastScan = g.Max(s => s.ScannedAt) })
            .ToDictionaryAsync(x => x.PassId, x => x.LastScan);

        var lastExits = await _context.VisitorScans
            .Where(s => passIds.Contains(s.VisitorPassId) && s.ScanType == "EXIT" && s.Status == "ACCEPTED")
            .GroupBy(s => s.VisitorPassId)
            .Select(g => new { PassId = g.Key, LastScan = g.Max(s => s.ScannedAt) })
            .ToDictionaryAsync(x => x.PassId, x => x.LastScan);

        Passes = passes.Select(p => new VisitorPassViewModel
        {
            Id = p.Id,
            PassNumber = p.PassNumber,
            Code = p.Code,
            CurrentStatus = p.CurrentStatus,
            IsActive = p.IsActive,
            IssuedAt = p.IssuedAt,
            LastEntry = lastEntries.TryGetValue(p.Id, out var le) ? _timezoneService.ToPhilippinesTime(le) : null,
            LastExit = lastExits.TryGetValue(p.Id, out var lx) ? _timezoneService.ToPhilippinesTime(lx) : null
        }).ToList();
    }

    public class VisitorPassViewModel
    {
        public Guid Id { get; set; }
        public int PassNumber { get; set; }
        public string Code { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime? LastEntry { get; set; }
        public DateTime? LastExit { get; set; }
    }
}
