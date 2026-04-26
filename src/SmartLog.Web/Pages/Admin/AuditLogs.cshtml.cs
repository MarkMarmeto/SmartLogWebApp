using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Audit log viewer page.
/// Implements US0050 (Audit Log Viewer) and US0051 (Audit Log Search and Filter).
/// US0099: LegalHold toggle and bulk-hold action.
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class AuditLogsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuditLogsModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public List<AuditLogEntry> AuditEntries { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<string> Users { get; set; } = new();
    public int HeldCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActionFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }

    public async Task OnGetAsync()
    {
        // US0050-AC2: Default shows last 24 hours
        var defaultStartDate = StartDate ?? DateTime.UtcNow.AddDays(-1);
        var defaultEndDate = EndDate ?? DateTime.UtcNow;

        // Build query
        var query = _context.AuditLogs
            .Include(a => a.User)
            .Include(a => a.PerformedByUser)
            .Where(a => a.Timestamp >= defaultStartDate && a.Timestamp <= defaultEndDate);

        // US0051: Apply filters
        if (!string.IsNullOrWhiteSpace(ActionFilter))
        {
            query = query.Where(a => a.Action == ActionFilter);
        }

        if (!string.IsNullOrWhiteSpace(UserFilter))
        {
            query = query.Where(a => a.PerformedByUserId == UserFilter);
        }

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var search = SearchTerm.ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(search) ||
                (a.Details != null && a.Details.ToLower().Contains(search)));
        }

        // Get total count
        TotalRecords = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalRecords / 50.0);

        // US0050-AC2: Sort by timestamp (newest first), paginated (50 per page)
        AuditEntries = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((PageNumber - 1) * 50)
            .Take(50)
            .Select(a => new AuditLogEntry
            {
                Id = a.Id,
                Timestamp = a.Timestamp,
                Action = a.Action,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.UserName : null,
                PerformedByUserId = a.PerformedByUserId,
                PerformedByUserName = a.PerformedByUser != null ? a.PerformedByUser.UserName : "System",
                Details = a.Details,
                IpAddress = a.IpAddress,
                LegalHold = a.LegalHold
            })
            .ToListAsync();

        HeldCount = await _context.AuditLogs.CountAsync(a => a.LegalHold);

        // Load filter options
        await LoadFiltersAsync();
    }

    // US0099: Per-row legal hold toggle
    public async Task<IActionResult> OnPostToggleLegalHoldAsync(Guid id)
    {
        var row = await _context.AuditLogs.FindAsync(id);
        if (row is null)
            return NotFound();

        var currentUserId = _userManager.GetUserId(User);
        var newHoldValue = !row.LegalHold;
        row.LegalHold = newHoldValue;

        _context.AuditLogs.Add(new AuditLog
        {
            Action = newHoldValue ? "AuditLegalHoldSet" : "AuditLegalHoldCleared",
            UserId = row.UserId,
            PerformedByUserId = currentUserId,
            Details = $"AuditLogId: {id}",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return RedirectToPage();
    }

    // US0099: Bulk legal hold on current filtered set
    public async Task<IActionResult> OnPostBulkLegalHoldAsync()
    {
        var defaultStartDate = StartDate ?? DateTime.UtcNow.AddDays(-1);
        var defaultEndDate = EndDate ?? DateTime.UtcNow;

        var query = _context.AuditLogs
            .Where(a => a.Timestamp >= defaultStartDate && a.Timestamp <= defaultEndDate);

        if (!string.IsNullOrWhiteSpace(ActionFilter))
            query = query.Where(a => a.Action == ActionFilter);
        if (!string.IsNullOrWhiteSpace(UserFilter))
            query = query.Where(a => a.PerformedByUserId == UserFilter);
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var search = SearchTerm.ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(search) ||
                (a.Details != null && a.Details.ToLower().Contains(search)));
        }

        var toHold = await query.Where(a => !a.LegalHold).ToListAsync();
        foreach (var log in toHold)
            log.LegalHold = true;

        var currentUserId = _userManager.GetUserId(User);
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "AuditBulkLegalHoldApplied",
            PerformedByUserId = currentUserId,
            Details = $"StartDate: {defaultStartDate:yyyy-MM-dd}, EndDate: {defaultEndDate:yyyy-MM-dd}, Action: {ActionFilter}, User: {UserFilter}, RowsAffected: {toHold.Count}",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadFiltersAsync()
    {
        // Get unique actions
        Actions = await _context.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync();

        // Get users who have performed actions
        var userIds = await _context.AuditLogs
            .Where(a => a.PerformedByUserId != null)
            .Select(a => a.PerformedByUserId)
            .Distinct()
            .ToListAsync();

        var users = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName })
            .ToListAsync();

        Users = users.Select(u => $"{u.UserName}|{u.Id}").ToList();
    }

    public class AuditLogEntry
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? PerformedByUserId { get; set; }
        public string? PerformedByUserName { get; set; }
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public bool LegalHold { get; set; }

        public string TruncatedDetails => Details != null && Details.Length > 100
            ? Details.Substring(0, 100) + "..."
            : Details ?? "-";
    }
}
