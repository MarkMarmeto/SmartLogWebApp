using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services.Sms;

namespace SmartLog.Web.Pages.Admin.Sms;

[Authorize(Policy = "RequireAdmin")]
public class QueueModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ISmsService _smsService;
    private readonly ILogger<QueueModel> _logger;

    public QueueModel(
        ApplicationDbContext context,
        ISmsService smsService,
        ILogger<QueueModel> logger)
    {
        _context = context;
        _smsService = smsService;
        _logger = logger;
    }

    public List<SmsQueue> QueuedMessages { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? MessageTypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PhoneSearch { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 50;
    public int TotalPages { get; set; }
    public int TotalMessages { get; set; }

    /// <summary>US0057: Count of Pending messages by MessageType (always all-type, not filtered).</summary>
    public Dictionary<string, int> PendingCountByType { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        // US0057: Summary counts (always from all pending, not affected by current filter)
        PendingCountByType = await _context.SmsQueues
            .Where(q => q.Status == SmsStatus.Pending)
            .GroupBy(q => q.MessageType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        var query = _context.SmsQueues.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(StatusFilter) && int.TryParse(StatusFilter, out var status))
        {
            query = query.Where(q => (int)q.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(MessageTypeFilter))
        {
            query = query.Where(q => q.MessageType == MessageTypeFilter);
        }

        if (!string.IsNullOrWhiteSpace(PhoneSearch))
        {
            query = query.Where(q => q.PhoneNumber.Contains(PhoneSearch));
        }

        // Get total count for pagination
        TotalMessages = await query.CountAsync();
        TotalPages = (int)Math.Ceiling(TotalMessages / (double)PageSize);

        // Ensure page number is valid
        if (PageNumber < 1) PageNumber = 1;
        if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

        // Get paginated results
        QueuedMessages = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostCancelAsync(long queueId)
    {
        try
        {
            var cancelled = await _smsService.CancelSmsAsync(queueId);
            if (cancelled)
            {
                StatusMessage = "SMS message cancelled successfully.";
                _logger.LogInformation("SMS {QueueId} cancelled", queueId);
            }
            else
            {
                StatusMessage = "Failed to cancel SMS message.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error cancelling SMS message.";
            _logger.LogError(ex, "Error cancelling SMS {QueueId}", queueId);
        }

        return RedirectToPage();
    }
}
