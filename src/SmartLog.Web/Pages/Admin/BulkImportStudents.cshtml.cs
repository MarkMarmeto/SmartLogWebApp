using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Services;

namespace SmartLog.Web.Pages.Admin;

[Authorize(Policy = "CanManageStudents")]
public class BulkImportStudentsModel : PageModel
{
    private readonly IBulkImportService _importService;
    private readonly UserManager<ApplicationUser> _userManager;

    public BulkImportStudentsModel(IBulkImportService importService, UserManager<ApplicationUser> userManager)
    {
        _importService = importService;
        _userManager = userManager;
    }

    [BindProperty]
    public IFormFile? ExcelFile { get; set; }

    public ImportValidationResult? ValidationResult { get; set; }
    public ImportResult? ImportResultData { get; set; }
    public string CurrentStep { get; set; } = "upload";

    [TempData]
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public IActionResult OnGetDownloadTemplate()
    {
        var bytes = _importService.GenerateStudentTemplate();
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "student_import_template.xlsx");
    }

    public async Task<IActionResult> OnPostValidateAsync()
    {
        if (ExcelFile == null || ExcelFile.Length == 0)
        {
            ErrorMessage = "Please select an Excel file to upload.";
            return Page();
        }

        var ext = Path.GetExtension(ExcelFile.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            ErrorMessage = "Please upload an Excel file (.xlsx or .xls).";
            return Page();
        }

        using var stream = ExcelFile.OpenReadStream();
        ValidationResult = await _importService.ValidateStudentXlsxAsync(stream);
        CurrentStep = "preview";

        if (ValidationResult.ValidCount > 0)
        {
            var validRows = ValidationResult.ValidStudentRows.Where(r => r.IsValid).Select(r => r.Row).ToList();
            TempData["ValidStudentRows"] = System.Text.Json.JsonSerializer.Serialize(validRows);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        var rowsJson = TempData["ValidStudentRows"] as string;
        if (string.IsNullOrEmpty(rowsJson))
        {
            ErrorMessage = "No validated data found. Please upload and validate your Excel file again.";
            return Page();
        }

        var validRows = System.Text.Json.JsonSerializer.Deserialize<List<StudentImportRow>>(rowsJson);
        if (validRows == null || validRows.Count == 0)
        {
            ErrorMessage = "No valid rows to import.";
            return Page();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        ImportResultData = await _importService.ImportStudentsAsync(validRows, currentUser?.Id ?? "");
        CurrentStep = "results";

        return Page();
    }
}
