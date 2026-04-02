using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "RequireAdmin")]
public class BatchReenrollmentModel : PageModel
{
    private readonly IBatchReenrollmentService _reenrollmentService;
    private readonly IAcademicYearService _academicYearService;
    private readonly UserManager<ApplicationUser> _userManager;

    public BatchReenrollmentModel(
        IBatchReenrollmentService reenrollmentService,
        IAcademicYearService academicYearService,
        UserManager<ApplicationUser> userManager)
    {
        _reenrollmentService = reenrollmentService;
        _academicYearService = academicYearService;
        _userManager = userManager;
    }

    public List<SelectListItem> AcademicYears { get; set; } = new();

    [BindProperty]
    public Guid SourceYearId { get; set; }

    [BindProperty]
    public Guid TargetYearId { get; set; }

    public string CurrentStep { get; set; } = "select";
    public ReenrollmentPreview? Preview { get; set; }
    public ReenrollmentResult? ResultData { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAcademicYearsAsync();
    }

    public async Task<IActionResult> OnPostPreviewAsync()
    {
        await LoadAcademicYearsAsync();

        if (SourceYearId == TargetYearId)
        {
            ErrorMessage = "Source and target academic years must be different.";
            return Page();
        }

        try
        {
            Preview = await _reenrollmentService.GeneratePreviewAsync(SourceYearId, TargetYearId);
            CurrentStep = "preview";

            // Store preview data in TempData for the execute step
            TempData["PreviewSourceYearId"] = SourceYearId;
            TempData["PreviewTargetYearId"] = TargetYearId;
            TempData["PreviewAssignments"] = System.Text.Json.JsonSerializer.Serialize(
                Preview.Students
                    .Where(s => s.Action != PromotionAction.Skip)
                    .Select(s => new StudentPromotionAssignment
                    {
                        StudentId = s.StudentId,
                        Action = s.Action,
                        SectionId = s.AssignedSectionId
                    }).ToList());
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostExecuteAsync()
    {
        await LoadAcademicYearsAsync();

        var sourceId = TempData["PreviewSourceYearId"] is Guid srcGuid ? srcGuid : SourceYearId;
        var targetId = TempData["PreviewTargetYearId"] is Guid tgtGuid ? tgtGuid : TargetYearId;
        var assignmentsJson = TempData["PreviewAssignments"] as string;

        if (string.IsNullOrEmpty(assignmentsJson))
        {
            ErrorMessage = "No preview data found. Please generate a preview first.";
            return Page();
        }

        var assignments = System.Text.Json.JsonSerializer.Deserialize<List<StudentPromotionAssignment>>(assignmentsJson);
        if (assignments == null || assignments.Count == 0)
        {
            ErrorMessage = "No students to process.";
            return Page();
        }

        // Apply section overrides from form
        foreach (var assignment in assignments.Where(a => a.Action == PromotionAction.Promote))
        {
            var formKey = $"SectionOverride_{assignment.StudentId}";
            if (Request.Form.TryGetValue(formKey, out var sectionValue) &&
                Guid.TryParse(sectionValue, out var sectionId))
            {
                assignment.SectionId = sectionId;
            }
        }

        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            ResultData = await _reenrollmentService.ExecuteReenrollmentAsync(
                sourceId, targetId, assignments, currentUser?.Id ?? "");
            CurrentStep = "results";
            SourceYearId = sourceId;
            TargetYearId = targetId;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    private async Task LoadAcademicYearsAsync()
    {
        var years = await _academicYearService.GetAllAcademicYearsAsync(activeOnly: true);
        AcademicYears = years.Select(y => new SelectListItem
        {
            Value = y.Id.ToString(),
            Text = y.Name + (y.IsCurrent ? " (Current)" : "")
        }).ToList();
    }
}
