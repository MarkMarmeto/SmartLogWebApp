using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Register scanner device page.
/// Implements US0028 (Register Scanner Device).
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class RegisterDeviceModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDeviceService _deviceService;
    private readonly IAuditService _auditService;

    public RegisterDeviceModel(
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

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? GeneratedApiKey { get; set; }
    public Guid? RegisteredDeviceId { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required]
        [StringLength(100, ErrorMessage = "Device name cannot exceed 100 characters.")]
        [Display(Name = "Device Name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Location")]
        public string Location { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Check if device name already exists (US0028-AC2)
        var existingDevice = await _context.Devices
            .FirstOrDefaultAsync(d => d.Name == Input.Name);

        if (existingDevice != null)
        {
            ModelState.AddModelError("Input.Name", "Device name already exists");
            return Page();
        }

        // Generate API key (US0028-AC3)
        var apiKey = _deviceService.GenerateApiKey();
        var apiKeyHash = _deviceService.HashApiKey(apiKey);

        // Get current user
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return RedirectToPage("/Account/Login");
        }

        // Create device record (US0028-AC5)
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = Input.Name,
            Location = Input.Location,
            Description = Input.Description,
            ApiKeyHash = apiKeyHash,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow,
            RegisteredBy = currentUser.Id
        };

        _context.Devices.Add(device);
        await _context.SaveChangesAsync();

        // Audit log (US0028-AC6)
        await _auditService.LogAsync(
            action: "DeviceRegistered",
            userId: null,
            performedByUserId: currentUser.Id,
            details: $"Registered device: {device.Name} at {device.Location}",
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        // Set the generated API key for display (US0028-AC3)
        GeneratedApiKey = apiKey;
        RegisteredDeviceId = device.Id;

        return Page();
    }
}
