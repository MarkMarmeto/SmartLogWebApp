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
/// Device list and management page.
/// Implements US0029 (Device List and Revocation).
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class DevicesModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDeviceService _deviceService;
    private readonly IAuditService _auditService;

    public DevicesModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IDeviceService deviceService,
        IAuditService auditService)
    {
        _context = context;
        _userManager = userManager;
        _deviceService = deviceService;
        _auditService = auditService;
    }

    public List<Device> Devices { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalDevices { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Newly regenerated API key (displayed once after regeneration).
    /// </summary>
    public string? RegeneratedApiKey { get; set; }

    /// <summary>
    /// Device ID for which the API key was regenerated.
    /// </summary>
    public Guid? RegeneratedDeviceId { get; set; }

    public async Task OnGetAsync()
    {
        var query = _context.Devices
            .Include(d => d.RegisteredByUser)
            .OrderByDescending(d => d.RegisteredAt);

        TotalDevices = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalDevices / (double)PageSize);

        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        Devices = await query
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostRevokeAsync(Guid id)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            return NotFound();
        }

        if (!device.IsActive)
        {
            StatusMessage = "Device is already revoked.";
            return RedirectToPage();
        }

        device.IsActive = false;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "DeviceRevoked",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Revoked device: {device.Name} (ID: {device.Id})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Device '{device.Name}' has been revoked.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReactivateAsync(Guid id)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            return NotFound();
        }

        if (device.IsActive)
        {
            StatusMessage = "Device is already active.";
            return RedirectToPage();
        }

        device.IsActive = true;
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "DeviceReactivated",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Reactivated device: {device.Name} (ID: {device.Id})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        StatusMessage = $"Device '{device.Name}' has been reactivated.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRegenerateKeyAsync(Guid id)
    {
        var device = await _context.Devices
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            return NotFound();
        }

        if (!device.IsActive)
        {
            StatusMessage = "Cannot regenerate API key for a revoked device. Reactivate it first.";
            return RedirectToPage();
        }

        // Generate new API key and update hash
        var newApiKey = _deviceService.GenerateApiKey();
        device.ApiKeyHash = _deviceService.HashApiKey(newApiKey);
        await _context.SaveChangesAsync();

        // Audit log
        var currentUser = await _userManager.GetUserAsync(User);
        await _auditService.LogAsync(
            action: "DeviceApiKeyRegenerated",
            userId: null,
            performedByUserId: currentUser?.Id,
            details: $"Regenerated API key for device: {device.Name} (ID: {device.Id})",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        // Show the new key (must reload page data since we're not redirecting)
        RegeneratedApiKey = newApiKey;
        RegeneratedDeviceId = device.Id;
        await OnGetAsync();
        return Page();
    }
}
