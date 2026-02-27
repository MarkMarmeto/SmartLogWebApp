using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Faculty list page with search and filter.
/// Implements US0026 (Faculty List with Search and Filter).
/// </summary>
[Authorize(Policy = "CanViewFaculty")]
public class FacultyModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private const int PageSize = 20;

    public FacultyModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Faculty> FacultyMembers { get; set; } = new();
    public List<string> Departments { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int TotalPages { get; set; }
    public int TotalFaculty { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DepartmentFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
    {
        PageNumber = pageNumber;

        // Predefined departments (US0023-AC5)
        Departments = new List<string>
        {
            "Mathematics",
            "Science",
            "English",
            "Filipino",
            "Social Studies",
            "Physical Education",
            "Arts",
            "Technology",
            "Administration",
            "Support Staff"
        };

        // Build query
        var query = _context.Faculties
            .Include(f => f.User)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchTerm))
        {
            var searchLower = SearchTerm.ToLower();
            query = query.Where(f =>
                f.FirstName.ToLower().Contains(searchLower) ||
                f.LastName.ToLower().Contains(searchLower) ||
                f.EmployeeId.ToLower().Contains(searchLower));
        }

        // Apply department filter
        if (!string.IsNullOrWhiteSpace(DepartmentFilter))
        {
            query = query.Where(f => f.Department == DepartmentFilter);
        }

        // Apply status filter
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            if (StatusFilter == "active")
            {
                query = query.Where(f => f.IsActive);
            }
            else if (StatusFilter == "inactive")
            {
                query = query.Where(f => !f.IsActive);
            }
        }

        // Get total count
        TotalFaculty = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalFaculty / (double)PageSize);

        // Apply pagination
        FacultyMembers = await query
            .OrderBy(f => f.LastName)
            .ThenBy(f => f.FirstName)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return Page();
    }
}
