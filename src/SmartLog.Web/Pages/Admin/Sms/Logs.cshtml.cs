using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class LogsModel : PageModel
{
    private readonly ApplicationDbContext _context;

    public LogsModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<SmsLog> Logs { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ProviderFilter { get; set; }

    /// <summary>US0057: Filter by message type.</summary>
    [BindProperty(SupportsGet = true)]
    public string? MessageTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PhoneSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalPages { get; set; }
    public int TotalLogs { get; set; }

    public async Task OnGetAsync()
    {
        var query = _context.SmsLogs.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(StatusFilter))
        {
            query = query.Where(l => l.Status == StatusFilter);
        }

        if (!string.IsNullOrWhiteSpace(ProviderFilter))
        {
            query = query.Where(l => l.Provider == ProviderFilter);
        }

        if (!string.IsNullOrWhiteSpace(MessageTypeFilter))
        {
            query = query.Where(l => l.MessageType == MessageTypeFilter);
        }

        if (!string.IsNullOrWhiteSpace(PhoneSearch))
        {
            query = query.Where(l => l.PhoneNumber.Contains(PhoneSearch));
        }

        if (StartDate.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= StartDate.Value);
        }

        if (EndDate.HasValue)
        {
            var endOfDay = EndDate.Value.AddDays(1);
            query = query.Where(l => l.CreatedAt < endOfDay);
        }

        // Get total count for pagination
        TotalLogs = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalLogs / (double)PageSize);

        // Ensure page number is valid
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        // Get paginated results
        Logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }
}
