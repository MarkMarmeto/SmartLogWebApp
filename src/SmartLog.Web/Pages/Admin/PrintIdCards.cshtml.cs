using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Models;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageStudents")]
public class PrintIdCardsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly IAppSettingsService _appSettings;

    public PrintIdCardsModel(ApplicationDbContext db, IAppSettingsService appSettings)
    {
        _db = db;
        _appSettings = appSettings;
    }

    public Section Section { get; set; } = null!;
    public List<StudentIdCardViewModel> Cards { get; set; } = [];
    public List<Student> SkippedStudents { get; set; } = [];

    public async Task<IActionResult> OnGetSectionAsync(Guid sectionId)
    {
        var section = await _db.Sections
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.Id == sectionId);

        if (section == null)
            return NotFound();

        Section = section;

        var students = await _db.StudentEnrollments
            .Where(e => e.SectionId == sectionId
                     && e.IsActive
                     && e.AcademicYear.IsCurrent)
            .Include(e => e.Student).ThenInclude(s => s.QrCodes)
            .Select(e => e.Student)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var schoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
        var schoolAddress = await _appSettings.GetAsync("Branding:SchoolAddress");
        var logoPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
        var returnAddress = await _appSettings.GetAsync("Branding:ReturnAddressText");

        foreach (var student in students)
        {
            var validQr = student.QrCodes.FirstOrDefault(q => q.IsValid);
            if (validQr == null)
            {
                SkippedStudents.Add(student);
                continue;
            }
            Cards.Add(new StudentIdCardViewModel
            {
                Student = student,
                QrCode = validQr,
                SchoolName = schoolName,
                SchoolAddress = schoolAddress,
                SchoolLogoPath = logoPath,
                ReturnAddressText = returnAddress,
            });
        }

        return Page();
    }
}
