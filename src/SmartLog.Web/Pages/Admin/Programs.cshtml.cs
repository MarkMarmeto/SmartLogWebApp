using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Services;
using Entities = SmartLog.Web.Data.Entities;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class ProgramsModel : PageModel
{
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ProgramsModel> _logger;

    public ProgramsModel(IGradeSectionService gradeSectionService, IAuditService auditService, ILogger<ProgramsModel> logger)
    {
        _gradeSectionService = gradeSectionService;
        _auditService = auditService;
        _logger = logger;
    }

    public List<Entities.Program> Programs { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Programs = await _gradeSectionService.GetAllProgramsAsync(activeOnly: false);
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        try
        {
            await _gradeSectionService.DeleteProgramAsync(id);
            await _auditService.LogAsync("DeleteProgram", null, null, $"Deleted program ID {id} by {User.Identity?.Name}");
            StatusMessage = "Program deleted successfully.";
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting program {Id}", id);
            ErrorMessage = "An unexpected error occurred while deleting the program.";
        }
        return RedirectToPage();
    }
}
