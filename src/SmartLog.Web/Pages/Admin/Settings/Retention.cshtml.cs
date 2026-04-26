using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using SmartLog.Web.Services.Retention;

namespace SmartLog.Web.Pages.Admin.Settings;

[Authorize(Policy = "RequireAdmin")]
public class RetentionModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly ILogger<RetentionModel> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    private static readonly Dictionary<string, int> MinFloors = new()
    {
        ["SmsQueue"]    = 7,
        ["SmsLog"]      = 30,
        ["Broadcast"]   = 30,
        ["Scan"]        = 365,
        ["AuditLog"]    = 365,
        ["VisitorScan"] = 7,
    };

    private static readonly Dictionary<string, (string FriendlyName, string Description)> EntityMeta = new()
    {
        ["SmsQueue"]    = ("SMS Queue",     "Outbound SMS messages awaiting or already sent"),
        ["SmsLog"]      = ("SMS Log",       "SMS delivery audit records"),
        ["Broadcast"]   = ("Broadcasts",    "Admin bulk-send announcements and emergency alerts"),
        ["Scan"]        = ("Scan Records",  "Student QR attendance scans"),
        ["AuditLog"]    = ("Audit Log",     "Security and admin action audit trail (RA 10173)"),
        ["VisitorScan"] = ("Visitor Scans", "Visitor entry/exit records"),
    };

    private static readonly string[] EntityOrder =
        ["SmsQueue", "SmsLog", "Broadcast", "Scan", "AuditLog", "VisitorScan"];

    [BindProperty]
    public List<RetentionPolicyViewModel> Policies { get; set; } = new();

    public List<RetentionRun> RecentRuns { get; set; } = new();
    public int HeldAuditLogCount { get; set; }
    public string ArchiveDirectory { get; set; } = "./archives";
    public string ArchiveDiskUsage { get; set; } = "—";

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public RetentionModel(ApplicationDbContext db, IAuditService audit, ILogger<RetentionModel> logger, IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _config = config;
    }

    public async Task OnGetAsync()
    {
        var policies = await _db.RetentionPolicies.AsNoTracking().ToListAsync();
        var policyMap = policies.ToDictionary(p => p.EntityName);

        Policies = EntityOrder
            .Where(name => policyMap.ContainsKey(name))
            .Select(name =>
            {
                var p = policyMap[name];
                var (friendlyName, description) = EntityMeta.GetValueOrDefault(name, (name, ""));
                return new RetentionPolicyViewModel
                {
                    Id            = p.Id,
                    EntityName    = p.EntityName,
                    FriendlyName  = friendlyName,
                    Description   = description,
                    RetentionDays = p.RetentionDays,
                    Enabled       = p.Enabled,
                    ArchiveEnabled = p.ArchiveEnabled,
                    LastRunAt     = p.LastRunAt,
                    LastRowsAffected = p.LastRowsAffected,
                    MinFloor      = MinFloors.GetValueOrDefault(p.EntityName, 1),
                };
            })
            .ToList();

        RecentRuns = await _db.RetentionRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(20)
            .ToListAsync();

        HeldAuditLogCount = await _db.AuditLogs.CountAsync(a => a.LegalHold);

        ArchiveDirectory = _config["Retention:ArchiveDirectory"] ?? "./archives";
        ArchiveDiskUsage = ComputeDiskUsage(ArchiveDirectory);
    }

    private static string ComputeDiskUsage(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
                return "0 B (directory not yet created)";

            var totalBytes = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(f => new FileInfo(f).Length);

            return totalBytes switch
            {
                < 1024 => $"{totalBytes} B",
                < 1024 * 1024 => $"{totalBytes / 1024.0:F1} KB",
                < 1024L * 1024 * 1024 => $"{totalBytes / (1024.0 * 1024):F1} MB",
                _ => $"{totalBytes / (1024.0 * 1024 * 1024):F2} GB"
            };
        }
        catch
        {
            return "N/A (unable to read directory)";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Server-side min-floor validation
        for (int i = 0; i < Policies.Count; i++)
        {
            var policy = Policies[i];
            if (MinFloors.TryGetValue(policy.EntityName, out var floor) && policy.RetentionDays < floor)
            {
                ModelState.AddModelError(
                    $"Policies[{i}].RetentionDays",
                    $"{policy.FriendlyName} requires at least {floor} days (regulatory minimum).");
            }
        }

        if (!ModelState.IsValid)
        {
            // Repopulate display-only fields that don't round-trip from the form
            await RepopulateDisplayFieldsAsync();
            await LoadRecentRunsAsync();
            return Page();
        }

        var performedBy = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        foreach (var vm in Policies)
        {
            var entity = await _db.RetentionPolicies.FindAsync(vm.Id);
            if (entity is null) continue;

            var oldDays    = entity.RetentionDays;
            var oldEnabled = entity.Enabled;
            var oldArchive = entity.ArchiveEnabled;

            entity.RetentionDays  = vm.RetentionDays;
            entity.Enabled        = vm.Enabled;
            entity.ArchiveEnabled = vm.ArchiveEnabled;
            entity.UpdatedAt      = DateTime.UtcNow;
            entity.UpdatedBy      = User.Identity?.Name;

            var changed = oldDays != vm.RetentionDays || oldEnabled != vm.Enabled || oldArchive != vm.ArchiveEnabled;
            if (changed)
            {
                await _audit.LogAsync(
                    action: "RetentionPolicyUpdated",
                    performedByUserId: performedBy,
                    details: $"Entity={vm.EntityName} RetentionDays: {oldDays}→{vm.RetentionDays} " +
                             $"Enabled: {oldEnabled}→{vm.Enabled} Archive: {oldArchive}→{vm.ArchiveEnabled}");
            }
        }

        await _db.SaveChangesAsync();
        StatusMessage = "Retention policies saved successfully.";
        _logger.LogInformation("Retention policies updated by {User}", User.Identity?.Name);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRunNowAsync(string entityName)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider
            .GetServices<IEntityRetentionHandler>()
            .FirstOrDefault(h => h.EntityName == entityName);

        if (handler is null)
        {
            ErrorMessage = $"No retention handler found for '{entityName}'.";
            return RedirectToPage();
        }

        var result = await handler.ExecuteAsync(dryRun: false, ct: HttpContext.RequestAborted, runMode: "Manual");

        if (result.Status == "Success")
            StatusMessage = $"Purge for {entityName}: {result.RowsAffected:N0} rows removed.";
        else if (result.Status == "Partial")
        {
            StatusMessage = $"Purge for {entityName} partially completed: {result.RowsAffected:N0} rows removed.";
            ErrorMessage = result.ErrorMessage;
        }
        else
            ErrorMessage = $"Purge for {entityName} failed. {result.ErrorMessage}";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDryRunAsync(string entityName)
    {
        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider
            .GetServices<IEntityRetentionHandler>()
            .FirstOrDefault(h => h.EntityName == entityName);

        if (handler is null)
        {
            ErrorMessage = $"No retention handler found for '{entityName}'.";
            return RedirectToPage();
        }

        var result = await handler.ExecuteAsync(dryRun: true, ct: HttpContext.RequestAborted, runMode: "DryRun");
        StatusMessage = $"Dry run for {entityName}: {result.RowsAffected:N0} rows would be affected.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetRunStatusAsync(string entityName)
    {
        var run = await _db.RetentionRuns
            .AsNoTracking()
            .Where(r => r.EntityName == entityName)
            .OrderByDescending(r => r.StartedAt)
            .FirstOrDefaultAsync();

        if (run is null) return new JsonResult(null);

        return new JsonResult(new
        {
            run.Status,
            run.RowsAffected,
            run.DurationMs,
            run.RunMode,
            StartedAt = run.StartedAt.ToString("HH:mm:ss"),
            CompletedAt = run.CompletedAt?.ToString("HH:mm:ss"),
            run.ErrorMessage
        });
    }

    private async Task RepopulateDisplayFieldsAsync()
    {
        var policies = await _db.RetentionPolicies.AsNoTracking().ToListAsync();
        var policyMap = policies.ToDictionary(p => p.EntityName);

        foreach (var vm in Policies)
        {
            var (friendlyName2, description2) = EntityMeta.GetValueOrDefault(vm.EntityName, (vm.EntityName, ""));
            vm.FriendlyName  = friendlyName2;
            vm.Description   = description2;
            vm.MinFloor      = MinFloors.GetValueOrDefault(vm.EntityName, 1);

            if (policyMap.TryGetValue(vm.EntityName, out var p))
            {
                vm.LastRunAt         = p.LastRunAt;
                vm.LastRowsAffected  = p.LastRowsAffected;
            }
        }
    }

    private async Task LoadRecentRunsAsync()
    {
        RecentRuns = await _db.RetentionRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(20)
            .ToListAsync();
    }
}

public class RetentionPolicyViewModel
{
    public int Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public bool Enabled { get; set; }
    public bool ArchiveEnabled { get; set; }
    public DateTime? LastRunAt { get; set; }
    public int? LastRowsAffected { get; set; }
    public int MinFloor { get; set; }
}
