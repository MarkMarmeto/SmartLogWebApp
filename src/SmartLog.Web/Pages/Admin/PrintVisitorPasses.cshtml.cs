using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

/// <summary>
/// Printable visitor pass QR cards page.
/// Implements US0074-AC6 (Print QR Cards).
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class PrintVisitorPassesModel : PageModel
{
    private readonly IVisitorPassService _visitorPassService;

    public PrintVisitorPassesModel(IVisitorPassService visitorPassService)
    {
        _visitorPassService = visitorPassService;
    }

    public List<VisitorPass> Passes { get; set; } = new();

    public async Task OnGetAsync(string? ids = null)
    {
        var allPasses = await _visitorPassService.GetAllAsync();

        if (!string.IsNullOrEmpty(ids))
        {
            var selectedIds = ids.Split(',')
                .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToHashSet();

            Passes = allPasses.Where(p => selectedIds.Contains(p.Id)).ToList();
        }
        else
        {
            Passes = allPasses.Where(p => p.IsActive).ToList();
        }
    }
}
