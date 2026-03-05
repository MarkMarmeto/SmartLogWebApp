using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Student list page with search and filter.
/// Implements US0018 (Student List with Search and Filter).
/// </summary>
[Authorize(Policy = "CanViewStudents")]
public class StudentsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public StudentsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Student> Students { get; set; } = new();
    public List<GradeLevel> GradeLevels { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? GradeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SectionFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;
    public int TotalPages { get; set; }
    public int TotalStudents { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
        GradeLevels = _context.GradeLevels
            .Where(g => g.IsActive)
            .OrderBy(g => g.SortOrder)
            .ToList();

        var query = _context.Students.AsQueryable();

        // Search by name or student ID
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            query = query.Where(s =>
                s.StudentId.ToLower().Contains(searchLower) ||
                s.FirstName.ToLower().Contains(searchLower) ||
                s.LastName.ToLower().Contains(searchLower));
        }

        // Filter by grade
        if (!string.IsNullOrWhiteSpace(GradeFilter))
        {
            query = query.Where(s => s.GradeLevel == GradeFilter);
        }

        // Filter by section
        if (!string.IsNullOrWhiteSpace(SectionFilter))
        {
            query = query.Where(s => s.Section.ToLower().Contains(SectionFilter.ToLower()));
        }

        // Filter by status
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            switch (StatusFilter.ToLower())
            {
                case "active":
                    query = query.Where(s => s.IsActive);
                    break;
                case "inactive":
                    query = query.Where(s => !s.IsActive);
                    break;
            }
        }

        // Get total count
        TotalStudents = query.Count();
        TotalPages = (int)Math.Ceiling(TotalStudents / (double)PageSize);

        // Ensure valid page number
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        // Pagination
        Students = query
            .OrderBy(s => s.GradeLevel.Length).ThenBy(s => s.GradeLevel)
            .ThenBy(s => s.Section)
            .ThenBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
    }
}
