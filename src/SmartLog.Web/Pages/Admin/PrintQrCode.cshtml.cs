using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

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
    public string? SchoolAddress { get; set; }
    public string? SchoolLogoPath { get; set; }
    public string? ReturnAddressText { get; set; }

    public StudentIdCardViewModel CardModel => new()
    {
        Student = Student,
        QrCode = QrCode,
        SchoolName = SchoolName,
        SchoolAddress = SchoolAddress,
        SchoolLogoPath = SchoolLogoPath,
        ReturnAddressText = ReturnAddressText,
    };

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var student = await _context.Students
            .Include(s => s.QrCodes)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound();

        var activeQrCode = student.QrCodes.FirstOrDefault(q => q.IsValid);
        if (activeQrCode == null)
            return BadRequest("No valid QR code available for this student");

        Student = student;
        QrCode = activeQrCode;
        SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
        SchoolAddress = await _appSettings.GetAsync("Branding:SchoolAddress");
        SchoolLogoPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
        ReturnAddressText = await _appSettings.GetAsync("Branding:ReturnAddressText");

        return Page();
    }
}
