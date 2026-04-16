using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Validation;

namespace SmartLog.Web.Services;

public class BulkImportService : IBulkImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IIdGenerationService _idGenerationService;
    private readonly IQrCodeService _qrCodeService;
    private readonly IGradeSectionService _gradeSectionService;
    private readonly IAcademicYearService _academicYearService;
    private readonly IAuditService _auditService;
    private readonly ILogger<BulkImportService> _logger;

    private static readonly HashSet<string> ValidDepartments = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mathematics", "Science", "English", "Filipino", "Social Studies",
        "Physical Education", "Arts", "Technology", "Administration", "Support Staff"
    };

    private static readonly HashSet<string> ValidRelationships = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mother", "Father", "Guardian", "Other"
    };

    private static readonly HashSet<string> ValidLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "EN", "FIL"
    };

    public BulkImportService(
        ApplicationDbContext context,
        IIdGenerationService idGenerationService,
        IQrCodeService qrCodeService,
        IGradeSectionService gradeSectionService,
        IAcademicYearService academicYearService,
        IAuditService auditService,
        ILogger<BulkImportService> logger)
    {
        _context = context;
        _idGenerationService = idGenerationService;
        _qrCodeService = qrCodeService;
        _gradeSectionService = gradeSectionService;
        _academicYearService = academicYearService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<ImportValidationResult> ValidateStudentXlsxAsync(Stream xlsxStream)
    {
        var result = new ImportValidationResult();

        // Parse rows from the first sheet
        List<List<string>> rows;
        try
        {
            rows = ParseXlsx(xlsxStream, sheetName: "Students");
        }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "File", Message = "Could not read Excel file: " + ex.Message });
            return result;
        }

        if (rows.Count == 0)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "File", Message = "No data rows found. Make sure you are using the provided template." });
            return result;
        }

        if (rows.Count > 500)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "File", Message = "Maximum 500 rows per import. File contains " + rows.Count + " rows." });
            return result;
        }

        var gradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
        var sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
        var existingLrns = await _context.Students.Where(s => s.LRN != null).Select(s => s.LRN!).ToListAsync();
        var lrnSet = new HashSet<string>(existingLrns);
        var fileLrns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        const int ColCount = 11;
        result.TotalRows = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            var fields = rows[i];
            var rowNum = i + 2; // +2: 1-based + header row
            var rowErrors = new List<ImportError>();

            while (fields.Count < ColCount)
                fields.Add("");

            var row = new StudentImportRow
            {
                RowNumber = rowNum,
                FirstName = fields[0].Trim(),
                LastName = fields[1].Trim(),
                MiddleName = string.IsNullOrWhiteSpace(fields[2]) ? null : fields[2].Trim(),
                GradeLevelCode = fields[3].Trim(),
                SectionName = fields[4].Trim(),
                ParentGuardianName = fields[5].Trim(),
                GuardianRelationship = fields[6].Trim(),
                ParentPhone = fields[7].Trim(),
                AlternatePhone = string.IsNullOrWhiteSpace(fields[8]) ? null : fields[8].Trim(),
                LRN = string.IsNullOrWhiteSpace(fields[9]) ? null : fields[9].Trim(),
                SmsLanguage = string.IsNullOrWhiteSpace(fields[10]) ? "EN" : fields[10].Trim().ToUpper()
            };

            if (string.IsNullOrWhiteSpace(row.FirstName))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "FirstName", Message = "First name is required" });
            if (string.IsNullOrWhiteSpace(row.LastName))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "LastName", Message = "Last name is required" });
            if (string.IsNullOrWhiteSpace(row.GradeLevelCode))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "GradeLevel", Message = "Grade level is required" });
            if (string.IsNullOrWhiteSpace(row.SectionName))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Section", Message = "Section is required" });
            if (string.IsNullOrWhiteSpace(row.ParentGuardianName))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "ParentGuardianName", Message = "Parent/Guardian name is required" });
            if (string.IsNullOrWhiteSpace(row.GuardianRelationship))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "GuardianRelationship", Message = "Guardian relationship is required" });
            else if (!ValidRelationships.Contains(row.GuardianRelationship))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "GuardianRelationship", Message = "Must be Mother, Father, Guardian, or Other", OriginalValue = row.GuardianRelationship });
            if (string.IsNullOrWhiteSpace(row.ParentPhone))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "ParentPhone", Message = "Parent phone is required" });
            else if (!PhMobileAttribute.IsValidPhMobile(row.ParentPhone))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "ParentPhone", Message = "Invalid Philippine mobile number (e.g. 09171234567)", OriginalValue = row.ParentPhone });

            if (!string.IsNullOrWhiteSpace(row.GradeLevelCode))
            {
                var gradeLevel = gradeLevels.FirstOrDefault(g =>
                    g.Code.Equals(row.GradeLevelCode, StringComparison.OrdinalIgnoreCase));
                if (gradeLevel == null)
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "GradeLevel", Message = $"Grade level '{row.GradeLevelCode}' not found or inactive", OriginalValue = row.GradeLevelCode });
                else if (!string.IsNullOrWhiteSpace(row.SectionName))
                {
                    var section = sections.FirstOrDefault(s =>
                        s.GradeLevelId == gradeLevel.Id &&
                        s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase));
                    if (section == null)
                        rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Section", Message = $"Section '{row.SectionName}' not found under grade {row.GradeLevelCode}. See 'Available Sections' sheet.", OriginalValue = row.SectionName });
                }
            }

            if (!string.IsNullOrWhiteSpace(row.LRN))
            {
                if (!Regex.IsMatch(row.LRN, @"^\d{12}$"))
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "LRN", Message = "LRN must be exactly 12 digits", OriginalValue = row.LRN });
                else if (lrnSet.Contains(row.LRN))
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "LRN", Message = "LRN already exists in database", OriginalValue = row.LRN });
                else if (!fileLrns.Add(row.LRN))
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "LRN", Message = "Duplicate LRN in import file", OriginalValue = row.LRN });
            }

            if (!ValidLanguages.Contains(row.SmsLanguage))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "SmsLanguage", Message = "Must be EN or FIL", OriginalValue = row.SmsLanguage });

            if (!string.IsNullOrWhiteSpace(row.AlternatePhone) && !PhMobileAttribute.IsValidPhMobile(row.AlternatePhone))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "AlternatePhone", Message = "Invalid Philippine mobile number (e.g. 09171234567)", OriginalValue = row.AlternatePhone });

            var validatedRow = new ValidatedRow<StudentImportRow>
            {
                Row = row,
                IsValid = rowErrors.Count == 0,
                RowErrors = rowErrors
            };

            result.ValidStudentRows.Add(validatedRow);
            result.Errors.AddRange(rowErrors);
        }

        result.ValidCount = result.ValidStudentRows.Count(r => r.IsValid);
        result.ErrorCount = result.ValidStudentRows.Count(r => !r.IsValid);
        return result;
    }

    public async Task<ImportResult> ImportStudentsAsync(List<StudentImportRow> validRows, string importedByUserId)
    {
        var sw = Stopwatch.StartNew();
        var result = new ImportResult();

        var gradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
        var sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
        var currentAcademicYear = await _academicYearService.GetCurrentAcademicYearAsync();

        if (currentAcademicYear == null)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "System", Message = "No current academic year found" });
            return result;
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            foreach (var row in validRows)
            {
                var gradeLevel = gradeLevels.First(g =>
                    g.Code.Equals(row.GradeLevelCode, StringComparison.OrdinalIgnoreCase));
                var section = sections.First(s =>
                    s.GradeLevelId == gradeLevel.Id &&
                    s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase));

                var studentId = await _idGenerationService.GenerateStudentIdAsync();

                var student = new Student
                {
                    StudentId = studentId,
                    LRN = row.LRN,
                    FirstName = row.FirstName,
                    MiddleName = row.MiddleName,
                    LastName = row.LastName,
                    GradeLevel = gradeLevel.Code,
                    Section = section.Name,
                    ParentGuardianName = row.ParentGuardianName,
                    GuardianRelationship = row.GuardianRelationship,
                    ParentPhone = row.ParentPhone,
                    AlternatePhone = row.AlternatePhone,
                    SmsLanguage = row.SmsLanguage,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Students.Add(student);
                await _context.SaveChangesAsync();

                // Enroll student
                await _gradeSectionService.EnrollStudentAsync(student.Id, section.Id, currentAcademicYear.Id);

                // Generate QR code
                var qrCode = await _qrCodeService.GenerateQrCodeAsync(student.StudentId);
                qrCode.StudentId = student.Id;
                _context.QrCodes.Add(qrCode);
                await _context.SaveChangesAsync();

                result.TotalImported++;
            }

            await transaction.CommitAsync();

            // Audit log
            await _auditService.LogAsync(
                action: "BulkStudentImport",
                performedByUserId: importedByUserId,
                details: $"Imported {result.TotalImported} students via bulk import");

            _logger.LogInformation("Bulk import completed: {Count} students imported by user {UserId}",
                result.TotalImported, importedByUserId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Bulk student import failed");
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "System", Message = "Import failed: " + ex.Message });
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public Task<ImportValidationResult> ValidateFacultyCsvAsync(Stream csvStream)
    {
        return Task.FromResult(ValidateFacultyCsvInternal(csvStream));
    }

    private ImportValidationResult ValidateFacultyCsvInternal(Stream csvStream)
    {
        var result = new ImportValidationResult();
        var rows = ParseCsv(csvStream);

        if (rows.Count == 0)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "File", Message = "CSV file is empty or has no data rows" });
            return result;
        }

        if (rows.Count > 200)
        {
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "File", Message = "Maximum 200 rows per import. File contains " + rows.Count + " rows." });
            return result;
        }

        var fileEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expectedHeaders = new[] { "FirstName", "LastName", "Department", "Position", "Email", "PhoneNumber", "HireDate", "ExternalEmployeeId" };

        result.TotalRows = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            var fields = rows[i];
            var rowNum = i + 2;
            var rowErrors = new List<ImportError>();

            while (fields.Count < expectedHeaders.Length)
                fields.Add("");

            var row = new FacultyImportRow
            {
                RowNumber = rowNum,
                FirstName = fields[0].Trim(),
                LastName = fields[1].Trim(),
                Department = fields[2].Trim(),
                Position = fields[3].Trim(),
                Email = string.IsNullOrWhiteSpace(fields[4]) ? null : fields[4].Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(fields[5]) ? null : fields[5].Trim(),
                HireDate = string.IsNullOrWhiteSpace(fields[6]) ? null : fields[6].Trim(),
                ExternalEmployeeId = string.IsNullOrWhiteSpace(fields[7]) ? null : fields[7].Trim()
            };

            // Required fields
            if (string.IsNullOrWhiteSpace(row.FirstName))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "FirstName", Message = "First name is required" });
            if (string.IsNullOrWhiteSpace(row.LastName))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "LastName", Message = "Last name is required" });
            if (string.IsNullOrWhiteSpace(row.Department))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Department", Message = "Department is required" });
            else if (!ValidDepartments.Contains(row.Department))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Department", Message = "Invalid department. Valid: " + string.Join(", ", ValidDepartments), OriginalValue = row.Department });
            if (string.IsNullOrWhiteSpace(row.Position))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Position", Message = "Position is required" });

            // Email validation
            if (!string.IsNullOrWhiteSpace(row.Email))
            {
                if (!Regex.IsMatch(row.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Email", Message = "Invalid email format", OriginalValue = row.Email });
                else if (!fileEmails.Add(row.Email))
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Email", Message = "Duplicate email in import file", OriginalValue = row.Email });
            }

            // Phone validation
            if (!string.IsNullOrWhiteSpace(row.PhoneNumber) && !Regex.IsMatch(row.PhoneNumber, @"^[\d\+\-\(\)\s]{7,20}$"))
                rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "PhoneNumber", Message = "Invalid phone number format", OriginalValue = row.PhoneNumber });

            // HireDate validation
            if (!string.IsNullOrWhiteSpace(row.HireDate))
            {
                if (!DateTime.TryParse(row.HireDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "HireDate", Message = "Invalid date format (use YYYY-MM-DD)", OriginalValue = row.HireDate });
            }

            var validatedRow = new ValidatedRow<FacultyImportRow>
            {
                Row = row,
                IsValid = rowErrors.Count == 0,
                RowErrors = rowErrors
            };

            result.ValidFacultyRows.Add(validatedRow);
            result.Errors.AddRange(rowErrors);
        }

        result.ValidCount = result.ValidFacultyRows.Count(r => r.IsValid);
        result.ErrorCount = result.ValidFacultyRows.Count(r => !r.IsValid);
        return result;
    }

    public async Task<ImportResult> ImportFacultyAsync(List<FacultyImportRow> validRows, string importedByUserId)
    {
        var sw = Stopwatch.StartNew();
        var result = new ImportResult();

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            foreach (var row in validRows)
            {
                var employeeId = await _idGenerationService.GenerateEmployeeIdAsync();

                DateTime? hireDate = null;
                if (!string.IsNullOrWhiteSpace(row.HireDate) &&
                    DateTime.TryParse(row.HireDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    hireDate = parsedDate;

                var faculty = new Faculty
                {
                    EmployeeId = employeeId,
                    ExternalEmployeeId = row.ExternalEmployeeId,
                    FirstName = row.FirstName,
                    LastName = row.LastName,
                    Department = row.Department,
                    Position = row.Position,
                    Email = row.Email,
                    PhoneNumber = row.PhoneNumber,
                    HireDate = hireDate,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Faculties.Add(faculty);
                await _context.SaveChangesAsync();

                result.TotalImported++;
            }

            await transaction.CommitAsync();

            await _auditService.LogAsync(
                action: "BulkFacultyImport",
                performedByUserId: importedByUserId,
                details: $"Imported {result.TotalImported} faculty members via bulk import");

            _logger.LogInformation("Bulk import completed: {Count} faculty imported by user {UserId}",
                result.TotalImported, importedByUserId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Bulk faculty import failed");
            result.Errors.Add(new ImportError { RowNumber = 0, Field = "System", Message = "Import failed: " + ex.Message });
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    public byte[] GenerateStudentTemplate()
    {
        using var wb = new XLWorkbook();

        // ── Sheet 1: Students (import data) ─────────────────────────────────
        var ws = wb.Worksheets.Add("Students");

        // Header row
        var headers = new[]
        {
            "FirstName", "LastName", "MiddleName", "GradeLevel", "Section",
            "ParentGuardianName", "GuardianRelationship", "ParentPhone",
            "AlternatePhone", "LRN", "SmsLanguage"
        };

        for (int c = 0; c < headers.Length; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2C7873");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Sample rows
        object[][] samples =
        {
            new object[] { "Juan",  "Dela Cruz", "Santos", "7",  "AGATE",       "Maria Dela Cruz", "Mother",   "09171234567", "",            "123456789012", "EN"  },
            new object[] { "Ana",   "Reyes",     "",       "K",  "SAMPAGUITA",  "Jose Reyes",      "Father",   "09281234567", "09181234567", "",             "FIL" },
            new object[] { "Pedro", "Santos",    "Cruz",   "11", "RUBY",        "Luz Santos",      "Guardian", "09391234567", "",            "",             "EN"  },
        };

        for (int r = 0; r < samples.Length; r++)
        {
            for (int c = 0; c < samples[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(samples[r][c]);
            ws.Row(r + 2).Style.Fill.BackgroundColor = r % 2 == 0 ? XLColor.FromHtml("#F8F9FA") : XLColor.White;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1); // Freeze header

        // ── Sheet 2: Available Sections (reference) ──────────────────────────
        var refWs = wb.Worksheets.Add("Available Sections");
        refWs.Cell(1, 1).Value = "Grade Level Code";
        refWs.Cell(1, 2).Value = "Grade Level Name";
        refWs.Cell(1, 3).Value = "Program Code";
        refWs.Cell(1, 4).Value = "Section Name";
        refWs.Cell(1, 5).Value = "Use in 'GradeLevel' column";
        refWs.Cell(1, 6).Value = "Use in 'Section' column";

        refWs.Row(1).Style.Font.Bold = true;
        refWs.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#17A2B8");
        refWs.Row(1).Style.Font.FontColor = XLColor.White;

        refWs.Cell(2, 1).Value = "(This sheet is auto-populated when you download from the app with existing sections.)";
        refWs.Cell(2, 1).Style.Font.Italic = true;
        refWs.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        refWs.Range("A2:F2").Merge();

        refWs.Columns().AdjustToContents();

        // ── Sheet 3: Instructions ────────────────────────────────────────────
        var instrWs = wb.Worksheets.Add("Instructions");
        var instrData = new[]
        {
            ("Column",          "Required", "Description"),
            ("FirstName",       "Yes",      "Student first name"),
            ("LastName",        "Yes",      "Student last name"),
            ("MiddleName",      "No",       "Student middle name (leave blank if none)"),
            ("GradeLevel",      "Yes",      "Grade level code: K, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, NG"),
            ("Section",         "Yes",      "Section name exactly as listed in 'Available Sections' sheet (e.g. AGATE, RUBY)"),
            ("ParentGuardianName","Yes",    "Full name of parent or guardian"),
            ("GuardianRelationship","Yes",  "One of: Mother, Father, Guardian, Other"),
            ("ParentPhone",     "Yes",      "Philippine mobile number (e.g. 09171234567)"),
            ("AlternatePhone",  "No",       "Second phone number (Philippine mobile format)"),
            ("LRN",             "No",       "Learner Reference Number — exactly 12 digits"),
            ("SmsLanguage",     "No",       "EN (English) or FIL (Filipino). Defaults to EN if blank"),
        };

        instrWs.Cell(1, 1).Value = "Column";
        instrWs.Cell(1, 2).Value = "Required";
        instrWs.Cell(1, 3).Value = "Description";
        instrWs.Row(1).Style.Font.Bold = true;
        instrWs.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#6C757D");
        instrWs.Row(1).Style.Font.FontColor = XLColor.White;

        for (int r = 1; r < instrData.Length; r++)
        {
            instrWs.Cell(r + 1, 1).Value = instrData[r].Item1;
            instrWs.Cell(r + 1, 2).Value = instrData[r].Item2;
            instrWs.Cell(r + 1, 3).Value = instrData[r].Item3;
            if (instrData[r].Item2 == "Yes")
                instrWs.Cell(r + 1, 2).Style.Font.FontColor = XLColor.Red;
            if (r % 2 == 0)
                instrWs.Row(r + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
        }

        instrWs.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] GenerateFacultyTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("FirstName,LastName,Department,Position,Email,PhoneNumber,HireDate,ExternalEmployeeId");
        sb.AppendLine("Maria,Santos,Mathematics,Senior Teacher,maria.santos@school.edu,09171234567,2020-06-01,OLD-001");
        sb.AppendLine("Jose,Reyes,Science,Teacher I,jose.reyes@school.edu,09281234567,,");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static List<List<string>> ParseXlsx(Stream stream, string sheetName = "Students")
    {
        var rows = new List<List<string>>();
        using var wb = new XLWorkbook(stream);

        var ws = wb.Worksheets.FirstOrDefault(s =>
            s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.First();

        var usedRange = ws.RangeUsed();
        if (usedRange == null) return rows;

        var firstRow = usedRange.FirstRow().RowNumber();
        var lastRow = usedRange.LastRow().RowNumber();
        var lastCol = usedRange.LastColumn().ColumnNumber();

        // Skip header (row 1), read from row 2
        for (int r = firstRow + 1; r <= lastRow; r++)
        {
            var fields = new List<string>();
            bool hasData = false;
            for (int c = 1; c <= Math.Max(lastCol, 11); c++)
            {
                var val = ws.Cell(r, c).GetValue<string>() ?? "";
                fields.Add(val);
                if (!string.IsNullOrWhiteSpace(val)) hasData = true;
            }
            if (hasData) rows.Add(fields);
        }

        return rows;
    }

    private static List<List<string>> ParseCsv(Stream stream)
    {
        var rows = new List<List<string>>();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // Skip header row
        var headerLine = reader.ReadLine();
        if (headerLine == null)
            return rows;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            rows.Add(ParseCsvLine(line));
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // Skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}
