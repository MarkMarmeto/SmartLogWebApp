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
    public IFormFile? CsvFile { get; set; }

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
        return File(bytes, "text/csv", "student_import_template.csv");
    }

    public async Task<IActionResult> OnPostValidateAsync()
    {
        if (CsvFile == null || CsvFile.Length == 0)
        {
            ErrorMessage = "Please select a CSV file to upload.";
            return Page();
        }

        if (!CsvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "Please upload a CSV file.";
            return Page();
        }

        using var stream = CsvFile.OpenReadStream();
        ValidationResult = await _importService.ValidateStudentCsvAsync(stream);
        CurrentStep = "preview";

        // Store valid rows in TempData for the import step
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
            ErrorMessage = "No validated data found. Please upload and validate your CSV file again.";
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
