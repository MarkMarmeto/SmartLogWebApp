using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartLog.Web.Data;
using SmartLog.Web.Data.Entities;
using SmartLog.Web.Validation;
using ProgramEntity = SmartLog.Web.Data.Entities.Program;

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

        List<string> headers;
        List<List<string>> rows;
        try
        {
            (headers, rows) = ParseXlsxWithHeaders(xlsxStream, sheetName: "Students");
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

        // Phase 3: detect legacy (11-col) vs current (12-col) template
        bool isLegacyTemplate = !headers.Any(h => h.Equals("Program", StringComparison.OrdinalIgnoreCase));

        int idxFirst = 0, idxLast = 1, idxMiddle = 2, idxGrade = 3;
        int idxProgram, idxSection, idxGuardianName, idxRelationship, idxParentPhone, idxAlt, idxLrn, idxLang;
        int colCount;

        if (isLegacyTemplate)
        {
            idxProgram = -1; idxSection = 4;
            idxGuardianName = 5; idxRelationship = 6; idxParentPhone = 7;
            idxAlt = 8; idxLrn = 9; idxLang = 10;
            colCount = 11;
        }
        else
        {
            idxProgram = 4; idxSection = 5;
            idxGuardianName = 6; idxRelationship = 7; idxParentPhone = 8;
            idxAlt = 9; idxLrn = 10; idxLang = 11;
            colCount = 12;
        }

        var gradeLevels = await _gradeSectionService.GetAllGradeLevelsAsync(activeOnly: true);
        var sections = await _gradeSectionService.GetAllSectionsAsync(activeOnly: true);
        var allPrograms = await _gradeSectionService.GetAllProgramsAsync(activeOnly: true);
        var gradeProgramLinks = await _context.GradeLevelPrograms.AsNoTracking().ToListAsync();
        var allowedProgramsByGrade = gradeProgramLinks
            .GroupBy(g => g.GradeLevelId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProgramId).ToHashSet());

        var existingLrns = await _context.Students.Where(s => s.LRN != null).Select(s => s.LRN!).ToListAsync();
        var lrnSet = new HashSet<string>(existingLrns);
        var fileLrns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        result.TotalRows = rows.Count;

        for (int i = 0; i < rows.Count; i++)
        {
            var fields = rows[i];
            var rowNum = i + 2;
            var rowErrors = new List<ImportError>();

            while (fields.Count < colCount)
                fields.Add("");

            var row = new StudentImportRow
            {
                RowNumber = rowNum,
                FirstName = fields[idxFirst].Trim(),
                LastName = fields[idxLast].Trim(),
                MiddleName = string.IsNullOrWhiteSpace(fields[idxMiddle]) ? null : fields[idxMiddle].Trim(),
                GradeLevelCode = fields[idxGrade].Trim(),
                ProgramCode = idxProgram >= 0 && !string.IsNullOrWhiteSpace(fields[idxProgram])
                    ? fields[idxProgram].Trim().ToUpperInvariant()
                    : null,
                SectionName = fields[idxSection].Trim(),
                ParentGuardianName = fields[idxGuardianName].Trim(),
                GuardianRelationship = fields[idxRelationship].Trim(),
                ParentPhone = fields[idxParentPhone].Trim(),
                AlternatePhone = string.IsNullOrWhiteSpace(fields[idxAlt]) ? null : fields[idxAlt].Trim(),
                LRN = string.IsNullOrWhiteSpace(fields[idxLrn]) ? null : fields[idxLrn].Trim(),
                SmsLanguage = string.IsNullOrWhiteSpace(fields[idxLang]) ? "EN" : fields[idxLang].Trim().ToUpper()
            };

            // Required field checks
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

            // Grade + Program + Section pipeline (Phase 4)
            if (!string.IsNullOrWhiteSpace(row.GradeLevelCode))
            {
                var gradeLevel = gradeLevels.FirstOrDefault(g =>
                    g.Code.Equals(row.GradeLevelCode, StringComparison.OrdinalIgnoreCase));

                if (gradeLevel == null)
                {
                    rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "GradeLevel", Message = $"Grade level '{row.GradeLevelCode}' not found or inactive", OriginalValue = row.GradeLevelCode });
                }
                else
                {
                    bool isNg = gradeLevel.Code.Equals("NG", StringComparison.OrdinalIgnoreCase);

                    // AC4 + AC5: Program required for graded, forbidden for NG
                    if (isNg && !string.IsNullOrEmpty(row.ProgramCode))
                    {
                        rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Program", Message = "Non-Graded rows must leave Program blank.", OriginalValue = row.ProgramCode });
                    }
                    else if (!isNg && string.IsNullOrEmpty(row.ProgramCode) && !isLegacyTemplate)
                    {
                        rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Program", Message = "Program is required for graded grade levels. See 'Available Sections' sheet." });
                    }

                    // AC6: Program must exist and be allowed for this grade (graded rows only)
                    ProgramEntity? program = null;
                    if (!isNg && !string.IsNullOrEmpty(row.ProgramCode))
                    {
                        program = allPrograms.FirstOrDefault(p =>
                            p.Code.Equals(row.ProgramCode, StringComparison.OrdinalIgnoreCase));

                        if (program == null)
                        {
                            rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Program", Message = $"Program '{row.ProgramCode}' not found or inactive.", OriginalValue = row.ProgramCode });
                        }
                        else if (!allowedProgramsByGrade.TryGetValue(gradeLevel.Id, out var allowed) || !allowed.Contains(program.Id))
                        {
                            rowErrors.Add(new ImportError { RowNumber = rowNum, Field = "Program", Message = $"Program '{program.Code}' is not allowed for grade '{gradeLevel.Code}'.", OriginalValue = row.ProgramCode });
                            program = null;
                        }
                    }

                    // AC7 / AC8 / AC9: Section resolution
                    if (!string.IsNullOrWhiteSpace(row.SectionName) && rowErrors.All(e => e.Field != "Program"))
                    {
                        List<Section> matches;

                        if (isNg)
                        {
                            // AC8: NG sections have no program
                            matches = sections.Where(s =>
                                s.GradeLevelId == gradeLevel.Id &&
                                s.ProgramId == null &&
                                s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }
                        else if (program != null)
                        {
                            // AC7: resolve by (grade, program, name)
                            matches = sections.Where(s =>
                                s.GradeLevelId == gradeLevel.Id &&
                                s.ProgramId == program.Id &&
                                s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }
                        else
                        {
                            // AC9: legacy mode, no program supplied — match by (grade, name)
                            matches = sections.Where(s =>
                                s.GradeLevelId == gradeLevel.Id &&
                                s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase))
                                .ToList();

                            if (matches.Count > 1)
                            {
                                rowErrors.Add(new ImportError
                                {
                                    RowNumber = rowNum,
                                    Field = "Program",
                                    Message = $"Section '{row.SectionName}' exists in multiple programs for grade '{gradeLevel.Code}'. Add a 'Program' column to disambiguate.",
                                    OriginalValue = row.SectionName
                                });
                                matches = new List<Section>();
                            }
                        }

                        if (rowErrors.All(e => e.Field != "Program") && matches.Count == 0)
                        {
                            var programLabel = isNg ? "—" : (program?.Code ?? "(none)");
                            rowErrors.Add(new ImportError
                            {
                                RowNumber = rowNum,
                                Field = "Section",
                                Message = $"Section '{row.SectionName}' not found under grade '{gradeLevel.Code}' / program '{programLabel}'.",
                                OriginalValue = row.SectionName
                            });
                        }
                        else if (matches.Count == 1 && isLegacyTemplate && !isNg && string.IsNullOrEmpty(row.ProgramCode))
                        {
                            // Backfill ProgramCode so ImportStudentsAsync resolves the same section
                            row.ProgramCode = matches[0].Program?.Code;
                        }
                    }
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

                bool isNg = gradeLevel.Code.Equals("NG", StringComparison.OrdinalIgnoreCase);
                Section section;
                if (isNg)
                {
                    section = sections.First(s =>
                        s.GradeLevelId == gradeLevel.Id &&
                        s.ProgramId == null &&
                        s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    section = sections.First(s =>
                        s.GradeLevelId == gradeLevel.Id &&
                        s.Program != null &&
                        s.Program.Code.Equals(row.ProgramCode, StringComparison.OrdinalIgnoreCase) &&
                        s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase));
                }

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

    public async Task<byte[]> GenerateStudentTemplateAsync()
    {
        using var wb = new XLWorkbook();

        // ── Sheet 1: Students (import data) ─────────────────────────────────
        var ws = wb.Worksheets.Add("Students");

        var headers = new[]
        {
            "FirstName", "LastName", "MiddleName", "GradeLevel", "Program", "Section",
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

        object[][] samples =
        {
            new object[] { "Juan",  "Dela Cruz", "Santos", "7",  "REGULAR", "AGATE",      "Maria Dela Cruz", "Mother",   "09171234567", "",            "123456789012", "EN"  },
            new object[] { "Ana",   "Reyes",     "",       "K",  "REGULAR", "SAMPAGUITA", "Jose Reyes",      "Father",   "09281234567", "09181234567", "",             "FIL" },
            new object[] { "Pedro", "Santos",    "Cruz",   "11", "STEM",    "RUBY",       "Luz Santos",      "Guardian", "09391234567", "",            "",             "EN"  },
            new object[] { "Liza",  "Cruz",      "",       "NG", "",        "LEVEL 1",    "Ana Cruz",        "Mother",   "09451234567", "",            "",             "EN"  },
        };

        for (int r = 0; r < samples.Length; r++)
        {
            for (int c = 0; c < samples[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(samples[r][c]);
            ws.Row(r + 2).Style.Fill.BackgroundColor = r % 2 == 0 ? XLColor.FromHtml("#F8F9FA") : XLColor.White;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        // ── Sheet 2: Available Sections (reference, populated from live data) ─
        var refWs = wb.Worksheets.Add("Available Sections");

        string[] refHeaders = { "Grade Level Code", "Grade Level Name", "Program Code", "Program Name", "Section Name" };
        for (int c = 0; c < refHeaders.Length; c++)
            refWs.Cell(1, c + 1).Value = refHeaders[c];
        refWs.Row(1).Style.Font.Bold = true;
        refWs.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#17A2B8");
        refWs.Row(1).Style.Font.FontColor = XLColor.White;

        await PopulateAvailableSectionsSheetAsync(refWs);

        // ── Sheet 3: Instructions ────────────────────────────────────────────
        var instrWs = wb.Worksheets.Add("Instructions");
        var instrData = new[]
        {
            ("Column",              "Required",     "Description"),
            ("FirstName",           "Yes",          "Student first name"),
            ("LastName",            "Yes",          "Student last name"),
            ("MiddleName",          "No",           "Student middle name (leave blank if none)"),
            ("GradeLevel",          "Yes",          "Grade level code: K, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, NG. NG = Non-Graded (SPED, ALS)."),
            ("Program",             "Conditional",  "Program code from the 'Available Sections' sheet (e.g. REGULAR, STEM, ABM). Required for graded grade levels. Must be blank for Non-Graded (NG) rows."),
            ("Section",             "Yes",          "Section name as listed in 'Available Sections' sheet. Section names may repeat across programs — use the Program column to disambiguate."),
            ("ParentGuardianName",  "Yes",          "Full name of parent or guardian"),
            ("GuardianRelationship","Yes",          "One of: Mother, Father, Guardian, Other"),
            ("ParentPhone",         "Yes",          "Philippine mobile number (e.g. 09171234567)"),
            ("AlternatePhone",      "No",           "Second phone number (Philippine mobile format)"),
            ("LRN",                 "No",           "Learner Reference Number — exactly 12 digits"),
            ("SmsLanguage",         "No",           "EN (English) or FIL (Filipino). Defaults to EN if blank"),
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
            else if (instrData[r].Item2 == "Conditional")
                instrWs.Cell(r + 1, 2).Style.Font.FontColor = XLColor.FromHtml("#B8860B");
            if (r % 2 == 0)
                instrWs.Row(r + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
        }

        instrWs.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task PopulateAvailableSectionsSheetAsync(IXLWorksheet ws)
    {
        var activeSections = await _context.Sections
            .Include(s => s.GradeLevel)
            .Include(s => s.Program)
            .Where(s => s.IsActive)
            .ToListAsync();

        var ordered = activeSections
            .OrderBy(s => s.GradeLevel.SortOrder)
            .ThenBy(s => s.Program == null ? int.MaxValue : s.Program.SortOrder)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int row = 2;
        foreach (var s in ordered)
        {
            ws.Cell(row, 1).Value = s.GradeLevel.Code;
            ws.Cell(row, 2).Value = s.GradeLevel.Name;
            ws.Cell(row, 3).Value = s.Program?.Code ?? "";
            ws.Cell(row, 4).Value = s.Program?.Name ?? "";
            ws.Cell(row, 5).Value = s.Name;
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
            row++;
        }

        if (row == 2)
        {
            ws.Cell(2, 1).Value = "(No active sections found. Create sections under Admin → Sections.)";
            ws.Cell(2, 1).Style.Font.Italic = true;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
            ws.Range("A2:E2").Merge();
        }

        ws.Columns().AdjustToContents();
    }

    public byte[] GenerateFacultyTemplate()
    {
        var sb = new StringBuilder();
        sb.AppendLine("FirstName,LastName,Department,Position,Email,PhoneNumber,HireDate,ExternalEmployeeId");
        sb.AppendLine("Maria,Santos,Mathematics,Senior Teacher,maria.santos@school.edu,09171234567,2020-06-01,OLD-001");
        sb.AppendLine("Jose,Reyes,Science,Teacher I,jose.reyes@school.edu,09281234567,,");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static (List<string> headers, List<List<string>> rows) ParseXlsxWithHeaders(Stream stream, string sheetName = "Students")
    {
        var headers = new List<string>();
        var rows = new List<List<string>>();
        using var wb = new XLWorkbook(stream);

        var ws = wb.Worksheets.FirstOrDefault(s =>
            s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
            ?? wb.Worksheets.First();

        var usedRange = ws.RangeUsed();
        if (usedRange == null) return (headers, rows);

        var firstRow = usedRange.FirstRow().RowNumber();
        var lastRow = usedRange.LastRow().RowNumber();
        var lastCol = usedRange.LastColumn().ColumnNumber();
        var widestCol = Math.Max(lastCol, 12);

        for (int c = 1; c <= widestCol; c++)
            headers.Add((ws.Cell(firstRow, c).GetValue<string>() ?? "").Trim());

        for (int r = firstRow + 1; r <= lastRow; r++)
        {
            var fields = new List<string>();
            bool hasData = false;
            for (int c = 1; c <= widestCol; c++)
            {
                var val = ws.Cell(r, c).GetValue<string>() ?? "";
                fields.Add(val);
                if (!string.IsNullOrWhiteSpace(val)) hasData = true;
            }
            if (hasData) rows.Add(fields);
        }

        return (headers, rows);
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
