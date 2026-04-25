using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Print enrollment stickers — per-year sticker showing S.Y., Grade, Program, Section.
/// Printed on back of student ID card each academic year.
/// Implements US0078 (Enrollment Sticker Print Page).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class PrintEnrollmentStickerModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IAppSettingsService _appSettings;
    private readonly IAcademicYearService _academicYearService;

    public PrintEnrollmentStickerModel(
        ApplicationDbContext context,
        IAppSettingsService appSettings,
        IAcademicYearService academicYearService)
    {
        _context = context;
        _appSettings = appSettings;
        _academicYearService = academicYearService;
    }

    public List<StickerEntry> Stickers { get; set; } = new();
    public string AcademicYear { get; set; } = string.Empty;
    public string SchoolName { get; set; } = "SmartLog School";

    /// <summary>Single student (from student profile page).</summary>
    public async Task<IActionResult> OnGetStudentAsync(Guid id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student == null) return NotFound();

        await PopulateCommonAsync();
        Stickers.Add(new StickerEntry(student, AcademicYear));
        return Page();
    }

    /// <summary>All active students in a section.</summary>
    public async Task<IActionResult> OnGetSectionAsync(Guid sectionId)
    {
        var students = await _context.Students
            .Where(s => s.IsActive && _context.StudentEnrollments
                .Any(e => e.StudentId == s.Id && e.SectionId == sectionId && e.IsActive))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        await PopulateCommonAsync();
        Stickers = students.Select(s => new StickerEntry(s, AcademicYear)).ToList();
        return Page();
    }

    /// <summary>All active students in a grade level.</summary>
    public async Task<IActionResult> OnGetGradeAsync(string grade)
    {
        var students = await _context.Students
            .Where(s => s.IsActive && s.GradeLevel == grade)
            .OrderBy(s => s.Section).ThenBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        await PopulateCommonAsync();
        Stickers = students.Select(s => new StickerEntry(s, AcademicYear)).ToList();
        return Page();
    }

    private async Task PopulateCommonAsync()
    {
        SchoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
        var currentYear = await _academicYearService.GetCurrentAcademicYearAsync();
        AcademicYear = currentYear?.Name ?? string.Empty;
    }
}

public record StickerEntry(Student Student, string AcademicYear)
{
    public string DisplayLine =>
        $"S.Y. {AcademicYear}  |  Grade {Student.GradeLevel}" +
        (string.IsNullOrEmpty(Student.Program) ? "" : $"  |  {Student.Program}") +
        $"  |  {Student.Section}";
}
