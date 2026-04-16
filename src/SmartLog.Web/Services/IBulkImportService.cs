namespace SmartLog.Web.Services;

public interface IBulkImportService
{
    Task<ImportValidationResult> ValidateStudentXlsxAsync(Stream xlsxStream);
    Task<ImportResult> ImportStudentsAsync(List<StudentImportRow> validRows, string importedByUserId);
    Task<ImportValidationResult> ValidateFacultyCsvAsync(Stream csvStream);
    Task<ImportResult> ImportFacultyAsync(List<FacultyImportRow> validRows, string importedByUserId);
    byte[] GenerateStudentTemplate();
    byte[] GenerateFacultyTemplate();
}

public class StudentImportRow
{
    public int RowNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string GradeLevelCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string ParentGuardianName { get; set; } = string.Empty;
    public string GuardianRelationship { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string? AlternatePhone { get; set; }
    public string? LRN { get; set; }
    public string SmsLanguage { get; set; } = "EN";
}

public class FacultyImportRow
{
    public int RowNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? HireDate { get; set; }
    public string? ExternalEmployeeId { get; set; }
}

public class ImportValidationResult
{
    public List<ValidatedRow<StudentImportRow>> ValidStudentRows { get; set; } = new();
    public List<ValidatedRow<FacultyImportRow>> ValidFacultyRows { get; set; } = new();
    public List<ImportError> Errors { get; set; } = new();
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
}

public class ValidatedRow<T>
{
    public T Row { get; set; } = default!;
    public bool IsValid { get; set; }
    public List<ImportError> RowErrors { get; set; } = new();
}

public class ImportResult
{
    public int TotalImported { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalErrors { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class ImportError
{
    public int RowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? OriginalValue { get; set; }
}
