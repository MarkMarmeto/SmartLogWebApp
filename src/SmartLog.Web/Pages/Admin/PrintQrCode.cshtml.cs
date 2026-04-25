using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Print QR code page — permanent CR80 ID card (no academic year).
/// Implements US0021 (Print Individual QR Code), US0077 (CR80 Card Template Redesign).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class PrintQrCodeModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAppSettingsService _appSettings;

    public PrintQrCodeModel(
        ApplicationDbContext context,
        IAppSettingsService appSettings)
    {
        _context = context;
        _appSettings = appSettings;
    }

    public Student Student { get; set; } = null!;
    public QrCode QrCode { get; set; } = null!;
    public string SchoolName { get; set; } = "SmartLog School";

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.QrCodes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        var activeQrCode = student.QrCodes.FirstOrDefault(q => q.IsValid);
        if (activeQrCode == null)
        {
            return BadRequest("No valid QR code available for this student");
        }

        Student = student;
        QrCode = activeQrCode;
        SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";

        return Page();
    }
}
