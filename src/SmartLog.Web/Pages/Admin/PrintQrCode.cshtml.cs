using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Print QR code page for individual student.
/// Implements US0021 (Print Individual QR Code).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class PrintQrCodeModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAppSettingsService _appSettings;
    private readonly IAcademicYearService _academicYearService;

    public PrintQrCodeModel(
        ApplicationDbContext context,
        IAppSettingsService appSettings,
        IAcademicYearService academicYearService)
    {
        _context = context;
        _appSettings = appSettings;
        _academicYearService = academicYearService;
    }

    public Student Student { get; set; } = null!;
    public QrCode QrCode { get; set; } = null!;
    public string SchoolName { get; set; } = "SmartLog School";
    public string AcademicYear { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.QrCode)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
        {
            return NotFound();
        }

        if (student.QrCode == null || !student.QrCode.IsValid)
        {
            return BadRequest("No valid QR code available for this student");
        }

        Student = student;
        QrCode = student.QrCode;
        SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";

        var currentYear = await _academicYearService.GetCurrentAcademicYearAsync();
        AcademicYear = currentYear?.Name ?? string.Empty;

        return Page();
    }
}
