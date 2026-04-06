using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Teacher;

[Authorize(Roles = "Teacher")]
public class StudentsModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentsModel(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Faculty? Faculty { get; set; }
    public Section? Section { get; set; }
    public List<Student> Students { get; set; } = new();
    public string? SearchTerm { get; set; }

    public async Task<IActionResult> OnGetAsync(string? search)
    {
        SearchTerm = search;

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        Faculty = await _context.Faculties
            .FirstOrDefaultAsync(f => f.UserId == user.Id);

        if (Faculty == null) return Page();

        Section = await _context.Sections
            .Include(s => s.GradeLevel)
            .FirstOrDefaultAsync(s => s.AdviserId == Faculty.Id && s.IsActive);

        if (Section == null) return Page();

        // Get active enrollments in this section for the current academic year
        var enrollmentQuery = _context.StudentEnrollments
            .Include(e => e.Student)
            .Where(e => e.SectionId == Section.Id && e.IsActive);

        var enrollments = await enrollmentQuery.ToListAsync();
        Students = enrollments.Select(e => e.Student).ToList();

        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var term = SearchTerm.ToLower();
            Students = Students.Where(s =>
                s.FullName.ToLower().Contains(term) ||
                s.StudentId.ToLower().Contains(term) ||
                (s.LRN != null && s.LRN.Contains(term))
            ).ToList();
        }

        Students = Students.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToList();

        return Page();
    }
}
