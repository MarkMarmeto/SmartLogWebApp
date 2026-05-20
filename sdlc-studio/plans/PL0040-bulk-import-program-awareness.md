# PL0040: Bulk Student Import — Program Awareness

> **Status:** Complete
> **Story:** [US0120: Bulk Student Import — Program Awareness](../stories/US0120-bulk-import-program-awareness.md)
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Created:** 2026-05-04
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8 + SQL Server (Razor Pages) + ClosedXML
> **Drafted by:** Claude (Opus 4.7)

---

## Overview

Make the bulk student import flow Program-aware end to end: add a `Program` column to the generated `student_import_template.xlsx`, populate the `Available Sections` reference sheet at download time, refresh the Instructions sheet, and tighten the validator to enforce the EP0010 rule set (Program required for graded rows, forbidden for NG, must be linked to the grade level via `GradeLevelProgram`, section resolved by `(Grade, Program, Name)`). Provide a backward-compatibility path for the existing 11-column template that auto-resolves Program when the `(Grade, Section)` combination is unambiguous and rejects the row with a clear error when it is not.

No DB schema changes. No changes to `ImportStudentsAsync` persistence — `GradeSectionService.EnrollStudentAsync` already denormalises `Student.Program` from the resolved section (`GradeSectionService.cs:430`) and does the right thing for NG (null).

---

## Acceptance Criteria Mapping

| AC (US0120) | Phase |
|-------------|-------|
| AC1: Template includes `Program` column | Phase 2 — `GenerateStudentTemplate` header + samples |
| AC2: `Available Sections` sheet populated at download | Phase 2 — query Sections + Programs + Grades |
| AC3: Instructions sheet documents Program & NG | Phase 2 — instruction rows |
| AC4: Validator — Program required for graded rows | Phase 4 — validator |
| AC5: Validator — Program forbidden for NG rows | Phase 4 — validator |
| AC6: Validator — Program code exists & linked to grade | Phase 4 — validator + `GradeSectionService` reuse |
| AC7: Section resolution by `(Grade, Program, Name)` for graded | Phase 4 — section lookup |
| AC8: Section resolution by `(Grade=NG, Name)` with null Program for NG | Phase 4 — section lookup |
| AC9: Backward compat for legacy 11-column template | Phase 3 — header detection + Phase 4 ambiguity branch |
| AC10: Persistence unchanged | Phase 5 — verify, no code change |

---

## Technical Context

### Current state (verified 2026-05-04)

**Service surface** — `src/SmartLog.Web/Services/BulkImportService.cs` is the single touch point:
- `ValidateStudentXlsxAsync` (line 57) — parses the `Students` sheet, runs per-row validation
- `ImportStudentsAsync` (line 186) — persists; calls `_gradeSectionService.EnrollStudentAsync` which sets `student.Program`
- `GenerateStudentTemplate` (line 431) — builds the xlsx with three sheets (`Students`, `Available Sections`, `Instructions`)

**DTO** — `StudentImportRow` lives in `src/SmartLog.Web/Services/IBulkImportService.cs:13-27`. Currently 11 fields; needs `Program` added (nullable string).

**Current parsing** — `ParseXlsx` (`BulkImportService.cs:546-577`) reads from row 2 with `lastCol` from `RangeUsed`, falling back to `Math.Max(lastCol, 11)`. The validator (`BulkImportService.cs:91`) hardcodes `ColCount = 11` and pads with empty strings. **The constant is the linchpin for AC9** — header inspection has to happen before column-count pinning.

**Section lookup (current)** — `BulkImportService.cs:146-148`:
```csharp
var section = sections.FirstOrDefault(s =>
    s.GradeLevelId == gradeLevel.Id &&
    s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase));
```
This is the silent-ambiguity bug the story documents.

**Section uniqueness** — `ApplicationDbContext.cs:203` declares a non-unique index on `(GradeLevelId, Name)`. So `(Grade 11 STEM, "RUBY")` and `(Grade 11 ABM, "RUBY")` are both legal.

**Program / GradeLevelProgram service** — `GradeSectionService` (verified `:218`, `:279`) exposes:
- `GetAllProgramsAsync(bool activeOnly)` — all programs
- `GetProgramsForGradeAsync(Guid gradeLevelId)` — programs linked to a grade via `GradeLevelProgram`

These are the right reuse points for AC6.

**NG semantics** — `GradeLevel.Code == "NG"`. Section under NG has `ProgramId = null`. `Student.Program` ends up null after enrollment (`GradeSectionService.cs:430`: `student.Program = section.Program?.Code`).

**Page handler** — `Pages/Admin/BulkImportStudents.cshtml.cs` is unchanged (TempData JSON round-trip of validated rows). Adding a `Program` field to `StudentImportRow` flows through serialization automatically — no page change required.

**No existing import tests** — `tests/SmartLog.Web.Tests/Services/` has nothing for `BulkImportService`. New test file lives there.

**ClosedXML** — already used for xlsx generation; reuse `XLWorkbook`, `XLColor`, etc. No new packages.

**Last migration:** none for this story — no schema change.

---

## Implementation Phases

### Phase 1 — DTO & Interface

**File:** `src/SmartLog.Web/Services/IBulkImportService.cs`

Add to `StudentImportRow` (after `SectionName`):
```csharp
public string? ProgramCode { get; set; }
```

Naming `ProgramCode` (not `Program`) for symmetry with `GradeLevelCode` and to avoid colliding with the entity name `SmartLog.Web.Data.Entities.Program`. The xlsx column header stays user-facing as `Program`.

No interface signature changes. `IBulkImportService` stays as-is.

**Build state after Phase 1:** clean — additive field.

### Phase 2 — Template generation (`GenerateStudentTemplate`)

**File:** `src/SmartLog.Web/Services/BulkImportService.cs`

#### 2a — `Students` sheet header + samples (AC1)

Update `headers` array (line 439) from 11 to 12 entries, inserting `Program` between `GradeLevel` and `Section`:
```csharp
var headers = new[]
{
    "FirstName", "LastName", "MiddleName", "GradeLevel", "Program", "Section",
    "ParentGuardianName", "GuardianRelationship", "ParentPhone",
    "AlternatePhone", "LRN", "SmsLanguage"
};
```

Update `samples` (line 458) with realistic values; include one NG row with blank Program:
```csharp
object[][] samples =
{
    new object[] { "Juan",  "Dela Cruz", "Santos", "7",  "REGULAR", "AGATE",      "Maria Dela Cruz", "Mother",   "09171234567", "",            "123456789012", "EN"  },
    new object[] { "Ana",   "Reyes",     "",       "K",  "REGULAR", "SAMPAGUITA", "Jose Reyes",      "Father",   "09281234567", "09181234567", "",             "FIL" },
    new object[] { "Pedro", "Santos",    "Cruz",   "11", "STEM",    "RUBY",       "Luz Santos",      "Guardian", "09391234567", "",            "",             "EN"  },
    new object[] { "Liza",  "Cruz",      "",       "NG", "",        "LEVEL 1",    "Ana Cruz",        "Mother",   "09451234567", "",            "",             "EN"  },
};
```

#### 2b — `Available Sections` sheet population (AC2)

Replace the placeholder block (lines 487-490) with a real population pass. Add a private helper:

```csharp
private async Task PopulateAvailableSectionsSheetAsync(IXLWorksheet ws)
{
    // Headers (already written by caller — keep in caller for styling consistency).
    var sections = await _context.Sections
        .Include(s => s.GradeLevel)
        .Include(s => s.Program)
        .Where(s => s.IsActive)
        .ToListAsync();

    var ordered = sections
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
        // Empty system — keep a single explanatory row rather than a blank sheet.
        ws.Cell(2, 1).Value = "(No active sections found. Create sections under Admin → Sections.)";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        ws.Range("A2:E2").Merge();
    }

    ws.Columns().AdjustToContents();
}
```

**Caller change** — replace lines 477-490 with:
```csharp
refWs.Cell(1, 1).Value = "Grade Level Code";
refWs.Cell(1, 2).Value = "Grade Level Name";
refWs.Cell(1, 3).Value = "Program Code";
refWs.Cell(1, 4).Value = "Program Name";
refWs.Cell(1, 5).Value = "Section Name";

refWs.Row(1).Style.Font.Bold = true;
refWs.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#17A2B8");
refWs.Row(1).Style.Font.FontColor = XLColor.White;

await PopulateAvailableSectionsSheetAsync(refWs);
```

**API impact** — `GenerateStudentTemplate` becomes async. Two options:

1. **Make `GenerateStudentTemplate` async** — change interface signature to `Task<byte[]> GenerateStudentTemplateAsync()` and update the caller `BulkImportStudents.cshtml.cs:38` to `await`.
2. **Synchronous query** — inline `_context.Sections.Include(...).Where(...).ToList()` (no async) inside the existing sync method.

**Decision:** option 1 — go async. The codebase consistently prefers async EF Core (`GetAllProgramsAsync`, `GetAllSectionsAsync`). Mixing sync I/O into a service that already has async methods invites future deadlocks. The page handler change is one line.

Update `IBulkImportService.cs:9`:
```csharp
Task<byte[]> GenerateStudentTemplateAsync();
```

Update `BulkImportStudents.cshtml.cs:36-40`:
```csharp
public async Task<IActionResult> OnGetDownloadTemplateAsync()
{
    var bytes = await _importService.GenerateStudentTemplateAsync();
    return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "student_import_template.xlsx");
}
```
Razor binding `asp-page-handler="DownloadTemplate"` works for `OnGetDownloadTemplateAsync` per ASP.NET conventions — no `.cshtml` markup change. Confirm by searching for `DownloadTemplate` references in cshtml during impl.

#### 2c — `Instructions` sheet (AC3)

Replace `instrData` (line 496) with:
```csharp
var instrData = new[]
{
    ("Column",          "Required",    "Description"),
    ("FirstName",       "Yes",         "Student first name"),
    ("LastName",        "Yes",         "Student last name"),
    ("MiddleName",      "No",          "Student middle name (leave blank if none)"),
    ("GradeLevel",      "Yes",         "Grade level code: K, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, NG. NG = Non-Graded (SPED, ALS)."),
    ("Program",         "Conditional", "Program code from the 'Available Sections' sheet (e.g. REGULAR, STEM, ABM). Required for graded grade levels. Must be blank for Non-Graded (NG) rows."),
    ("Section",         "Yes",         "Section name as listed in 'Available Sections' sheet (e.g. AGATE, RUBY). Section names may repeat across programs — use the Program column to disambiguate."),
    ("ParentGuardianName","Yes",       "Full name of parent or guardian"),
    ("GuardianRelationship","Yes",     "One of: Mother, Father, Guardian, Other"),
    ("ParentPhone",     "Yes",         "Philippine mobile number (e.g. 09171234567)"),
    ("AlternatePhone",  "No",          "Second phone number (Philippine mobile format)"),
    ("LRN",             "No",          "Learner Reference Number — exactly 12 digits"),
    ("SmsLanguage",     "No",          "EN (English) or FIL (Filipino). Defaults to EN if blank"),
};
```

The conditional-formatting rule that paints "Yes" red (line 524) should also colour "Conditional" amber for visual distinction:
```csharp
if (instrData[r].Item2 == "Yes")
    instrWs.Cell(r + 1, 2).Style.Font.FontColor = XLColor.Red;
else if (instrData[r].Item2 == "Conditional")
    instrWs.Cell(r + 1, 2).Style.Font.FontColor = XLColor.FromHtml("#B8860B"); // dark amber
```

#### 2d — Page copy

`Pages/Admin/BulkImportStudents.cshtml:66` — current text already mentions the Available Sections sheet; no change required. Optionally add a one-liner under the upload widget noting "The template now includes a Program column. Existing files exported before this update may still be uploaded — see the Instructions sheet." Out of scope unless the user asks.

### Phase 3 — Header detection & legacy mode (AC9 part 1)

**File:** `src/SmartLog.Web/Services/BulkImportService.cs`

Modify `ParseXlsx` to also return the header row, and let the caller decide whether the file is in `legacy` (no `Program` column) or `current` (has `Program`) mode.

```csharp
private static (List<string> headers, List<List<string>> rows) ParseXlsxWithHeaders(Stream stream, string sheetName = "Students")
{
    var rows = new List<List<string>>();
    var headers = new List<string>();
    using var wb = new XLWorkbook(stream);

    var ws = wb.Worksheets.FirstOrDefault(s =>
        s.Name.Equals(sheetName, StringComparison.OrdinalIgnoreCase))
        ?? wb.Worksheets.First();

    var usedRange = ws.RangeUsed();
    if (usedRange == null) return (headers, rows);

    var firstRow = usedRange.FirstRow().RowNumber();
    var lastRow = usedRange.LastRow().RowNumber();
    var lastCol = usedRange.LastColumn().ColumnNumber();
    var widestCol = Math.Max(lastCol, 12); // up from 11

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
```

Keep the existing `ParseXlsx` intact for any callers that don't need headers (none currently — the new method can fully replace it; delete `ParseXlsx` after refactor to keep one parser).

**Mode detection** in `ValidateStudentXlsxAsync` (top of method, after parse):
```csharp
var (headers, rows) = ParseXlsxWithHeaders(xlsxStream, "Students");

bool isLegacyTemplate = !headers.Any(h =>
    h.Equals("Program", StringComparison.OrdinalIgnoreCase));

// Column index map
int idxFirst = 0, idxLast = 1, idxMiddle = 2, idxGrade = 3;
int idxProgram = -1;     // -1 in legacy mode
int idxSection, idxGuardianName, idxRelationship, idxParentPhone, idxAlt, idxLrn, idxLang;

if (isLegacyTemplate)
{
    idxSection = 4;
    idxGuardianName = 5; idxRelationship = 6; idxParentPhone = 7;
    idxAlt = 8; idxLrn = 9; idxLang = 10;
}
else
{
    idxProgram = 4; idxSection = 5;
    idxGuardianName = 6; idxRelationship = 7; idxParentPhone = 8;
    idxAlt = 9; idxLrn = 10; idxLang = 11;
}
```

**Why positional, not header-name lookup?** The current parser is positional (`fields[3]`, `fields[4]`, etc.). Switching to a name-based map for the entire validator is a bigger refactor than this story warrants and risks regressions on locale-mangled headers. Two fixed shapes (legacy / current) is sufficient.

**Column-count cap** — replace `const int ColCount = 11;` (line 91) with:
```csharp
int colCount = isLegacyTemplate ? 11 : 12;
```
And update the padding loop accordingly.

### Phase 4 — Validator changes (AC4–AC8 + AC9 part 2)

**File:** `src/SmartLog.Web/Services/BulkImportService.cs`

#### 4a — Load Program data once per validation run

After loading `gradeLevels` and `sections` (lines 85-86), add:
```csharp
var allPrograms = await _gradeSectionService.GetAllProgramsAsync(activeOnly: true);

// program-id → list-of-allowed-grade-ids, materialized once
var gradeProgramLinks = await _context.GradeLevelPrograms.AsNoTracking().ToListAsync();
var allowedProgramsByGrade = gradeProgramLinks
    .GroupBy(g => g.GradeLevelId)
    .ToDictionary(g => g.Key, g => g.Select(x => x.ProgramId).ToHashSet());
```

`GradeLevelPrograms` is already an EF DbSet (`ApplicationDbContext.cs` — verified via repo grep at story-research time). Use `_context` directly rather than adding a service method, since this is a read-only join keyed by id.

#### 4b — Per-row validation flow

Replace the row-build + lookup block (current lines 103-152) with:

```csharp
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
```

Then keep the existing required-field, relationship, phone, LRN, language checks. Replace the grade-level + section block with the new pipeline:

```csharp
if (!string.IsNullOrWhiteSpace(row.GradeLevelCode))
{
    var gradeLevel = gradeLevels.FirstOrDefault(g =>
        g.Code.Equals(row.GradeLevelCode, StringComparison.OrdinalIgnoreCase));

    if (gradeLevel == null)
    {
        rowErrors.Add(new ImportError {
            RowNumber = rowNum, Field = "GradeLevel",
            Message = $"Grade level '{row.GradeLevelCode}' not found or inactive",
            OriginalValue = row.GradeLevelCode });
    }
    else
    {
        bool isNg = gradeLevel.Code.Equals("NG", StringComparison.OrdinalIgnoreCase);

        // ── AC4 + AC5: Program required for graded, forbidden for NG
        if (isNg && !string.IsNullOrEmpty(row.ProgramCode))
        {
            rowErrors.Add(new ImportError {
                RowNumber = rowNum, Field = "Program",
                Message = "Non-Graded rows must leave Program blank.",
                OriginalValue = row.ProgramCode });
        }
        else if (!isNg && string.IsNullOrEmpty(row.ProgramCode) && !isLegacyTemplate)
        {
            rowErrors.Add(new ImportError {
                RowNumber = rowNum, Field = "Program",
                Message = "Program is required for graded grade levels. See 'Available Sections' sheet." });
        }

        // ── AC6: Program must exist & be allowed for the grade (graded rows only, when supplied)
        Entities.Program? program = null;
        if (!isNg && !string.IsNullOrEmpty(row.ProgramCode))
        {
            program = allPrograms.FirstOrDefault(p =>
                p.Code.Equals(row.ProgramCode, StringComparison.OrdinalIgnoreCase));
            if (program == null)
            {
                rowErrors.Add(new ImportError {
                    RowNumber = rowNum, Field = "Program",
                    Message = $"Program '{row.ProgramCode}' not found or inactive.",
                    OriginalValue = row.ProgramCode });
            }
            else if (!allowedProgramsByGrade.TryGetValue(gradeLevel.Id, out var allowed)
                     || !allowed.Contains(program.Id))
            {
                rowErrors.Add(new ImportError {
                    RowNumber = rowNum, Field = "Program",
                    Message = $"Program '{program.Code}' is not allowed for grade '{gradeLevel.Code}'.",
                    OriginalValue = row.ProgramCode });
                program = null; // force section resolution to skip
            }
        }

        // ── AC7 / AC8 / AC9: Section resolution
        if (!string.IsNullOrWhiteSpace(row.SectionName) && rowErrors.All(e => e.Field != "Program"))
        {
            List<Section> matches;

            if (isNg)
            {
                matches = sections.Where(s =>
                    s.GradeLevelId == gradeLevel.Id &&
                    s.ProgramId == null &&
                    s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else if (program != null)
            {
                matches = sections.Where(s =>
                    s.GradeLevelId == gradeLevel.Id &&
                    s.ProgramId == program.Id &&
                    s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                // Legacy mode, graded row, no Program supplied — match by (Grade, Name) only.
                matches = sections.Where(s =>
                    s.GradeLevelId == gradeLevel.Id &&
                    s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matches.Count > 1)
                {
                    rowErrors.Add(new ImportError {
                        RowNumber = rowNum, Field = "Program",
                        Message = $"Section '{row.SectionName}' exists in multiple programs for grade '{gradeLevel.Code}'. Add a 'Program' column to disambiguate.",
                        OriginalValue = row.SectionName });
                    matches = new List<Section>(); // skip section "found" branch
                }
            }

            if (rowErrors.All(e => e.Field != "Program") && matches.Count == 0)
            {
                var programLabel = isNg ? "—" : (program?.Code ?? "(none)");
                rowErrors.Add(new ImportError {
                    RowNumber = rowNum, Field = "Section",
                    Message = $"Section '{row.SectionName}' not found under grade '{gradeLevel.Code}' / program '{programLabel}'.",
                    OriginalValue = row.SectionName });
            }
            else if (matches.Count == 1)
            {
                // Backfill ProgramCode for legacy rows so ImportStudentsAsync resolves the same section.
                if (isLegacyTemplate && !isNg && string.IsNullOrEmpty(row.ProgramCode))
                {
                    row.ProgramCode = matches[0].Program?.Code;
                }
            }
        }
    }
}
```

**Important detail — TempData round-trip.** `BulkImportStudents.cshtml.cs:64` serialises `validRows` to JSON in TempData and `ImportStudentsAsync` re-uses the deserialised rows. The validator backfilling `row.ProgramCode` for legacy-mode unambiguous matches means `ImportStudentsAsync` can use the **same** lookup logic without re-detecting legacy mode — clean separation.

#### 4c — `ImportStudentsAsync` lookup parity

Replace the two `First` calls (lines 207-211) with the new tuple lookup:

```csharp
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
            s.Name.Equals(row.SectionName, StringComparison.OrdinalIgnoreCase) &&
            s.Program != null &&
            s.Program.Code.Equals(row.ProgramCode, StringComparison.OrdinalIgnoreCase));
    }

    // …existing student creation + EnrollStudentAsync unchanged…
}
```

**`GetAllSectionsAsync`** must already include `Program` navigation. Verify at impl time — if not, add `.Include(s => s.Program)` to the helper or load fresh in this method.

**No change** to the rest of `ImportStudentsAsync`. `EnrollStudentAsync` still sets `student.Program = section.Program?.Code` — handles graded and NG correctly. (AC10.)

### Phase 5 — Tests

**New file:** `tests/SmartLog.Web.Tests/Services/BulkImportServiceTests.cs`

Tests use `TestDbContextFactory.SeedAll(context)` — confirmed at story research time that helper seeds Programs (REGULAR + at least one), GradeLevels, and Sections under REGULAR. Add a small per-test seed step for ambiguity / NG cases.

**Mocking surface** — `BulkImportService` ctor takes:
- `ApplicationDbContext` (real, in-memory)
- `IIdGenerationService` — stub returning incrementing studentIds
- `IQrCodeService` — stub returning a fake `QrCode`
- `IGradeSectionService` — **use the real one** wrapping the same in-memory context (it's pure logic over EF)
- `IAcademicYearService` — stub returning a current academic year
- `IAuditService` — Mock<IAuditService> with no-op `LogAsync`
- `ILogger<BulkImportService>` — `NullLogger`

#### Test cases

```csharp
public class BulkImportServiceTests
{
    [Fact]
    public async Task GenerateStudentTemplate_HasProgramColumnAndPopulatesAvailableSections() { /* ... */ }

    [Fact]
    public async Task Validate_NewTemplate_GradedRowMissingProgram_FailsAC4() { /* ... */ }

    [Fact]
    public async Task Validate_NewTemplate_NgRowWithProgram_FailsAC5() { /* ... */ }

    [Fact]
    public async Task Validate_UnknownProgramCode_FailsAC6() { /* ... */ }

    [Fact]
    public async Task Validate_ProgramNotLinkedToGrade_FailsAC6() { /* ... */ }

    [Fact]
    public async Task Validate_SectionNotUnderGradeProgram_FailsAC7() { /* ... */ }

    [Fact]
    public async Task Validate_NgSectionNotFound_FailsAC8() { /* ... */ }

    [Fact]
    public async Task Validate_LegacyTemplate_UnambiguousSection_AutoResolvesProgram() { /* ... */ }

    [Fact]
    public async Task Validate_LegacyTemplate_AmbiguousSection_FailsAC9() { /* ... */ }

    [Fact]
    public async Task Import_GradedRow_PersistsStudentProgramFromSection() { /* ... */ }

    [Fact]
    public async Task Import_NgRow_PersistsStudentProgramAsNull() { /* ... */ }

    [Fact]
    public async Task Validate_CaseInsensitive_ProgramAndSection_Matches() { /* ... */ }
}
```

#### Test fixture helpers

Add to `tests/SmartLog.Web.Tests/Helpers/TestDbContextFactory.cs` (only if needed by these tests; otherwise inline in the test class):

```csharp
public static void SeedAmbiguousSectionScenario(ApplicationDbContext context)
{
    // Grade 11 with two programs (STEM, ABM), each with a section called "RUBY".
    var grade11 = context.GradeLevels.First(g => g.Code == "11");
    var stem = new Program { Id = Guid.NewGuid(), Code = "STEM", Name = "Science, Tech, Engineering & Math", IsActive = true, SortOrder = 10 };
    var abm  = new Program { Id = Guid.NewGuid(), Code = "ABM",  Name = "Accountancy, Business & Mgmt", IsActive = true, SortOrder = 11 };
    context.Programs.AddRange(stem, abm);
    context.GradeLevelPrograms.AddRange(
        new GradeLevelProgram { GradeLevelId = grade11.Id, ProgramId = stem.Id },
        new GradeLevelProgram { GradeLevelId = grade11.Id, ProgramId = abm.Id });
    context.Sections.AddRange(
        new Section { Id = Guid.NewGuid(), Name = "RUBY", GradeLevelId = grade11.Id, ProgramId = stem.Id, IsActive = true },
        new Section { Id = Guid.NewGuid(), Name = "RUBY", GradeLevelId = grade11.Id, ProgramId = abm.Id,  IsActive = true });
    context.SaveChanges();
}

public static void SeedNgScenario(ApplicationDbContext context)
{
    var ng = new GradeLevel { Id = Guid.NewGuid(), Code = "NG", Name = "Non-Graded", IsActive = true, SortOrder = 99 };
    context.GradeLevels.Add(ng);
    context.Sections.Add(new Section { Id = Guid.NewGuid(), Name = "LEVEL 1", GradeLevelId = ng.Id, ProgramId = null, IsActive = true });
    context.SaveChanges();
}
```

If the existing `SeedAll` already covers either scenario, skip the helper and use it directly.

#### Building xlsx fixtures in tests

Use ClosedXML directly:
```csharp
private static MemoryStream BuildXlsx(string[] headers, params object[][] rows)
{
    var ms = new MemoryStream();
    using (var wb = new XLWorkbook())
    {
        var ws = wb.Worksheets.Add("Students");
        for (int c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
        wb.SaveAs(ms);
    }
    ms.Position = 0;
    return ms;
}
```

#### Manual verification checklist

Run against `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"`:

1. **Template download** — visit `/Admin/BulkImportStudents` → "Download Template". Open the file:
   - `Students` sheet has 12 columns; `Program` is column E.
   - 4 sample rows including one NG row with blank Program.
   - `Available Sections` sheet lists every active section with grade/program/section columns; NG sections show empty Program columns; rows are sorted by grade then program then name.
   - `Instructions` sheet has the new Program row marked "Conditional" in amber, and the GradeLevel description mentions NG.
2. **Happy import (new template)** — fill in 3 graded rows + 1 NG row referencing real sections, upload → preview shows all 4 valid → confirm import → verify `SELECT FirstName, GradeLevel, Program FROM Students` shows the right Program (or null for NG).
3. **AC4 — graded missing Program** — upload one graded row with blank Program → preview shows row error "Program is required for graded grade levels".
4. **AC5 — NG with Program** — upload an NG row with `Program=REGULAR` → preview shows error "Non-Graded rows must leave Program blank."
5. **AC6 — unknown Program** — `Program=XYZ` → preview shows "Program 'XYZ' not found or inactive."
6. **AC6 — Program not allowed for grade** — assuming Grade 1 isn't linked to STEM, set `GradeLevel=1, Program=STEM` → "Program 'STEM' is not allowed for grade '1'."
7. **AC7 — section not under (grade, program)** — `Grade=11, Program=STEM, Section=ABM-A` → "Section 'ABM-A' not found under grade '11' / program 'STEM'."
8. **AC8 — NG section not found** — `Grade=NG, Program=, Section=ZZ` → similar message with program shown as "—".
9. **AC9 happy — legacy template, unambiguous** — manually craft an 11-column xlsx (no Program header), upload → preview shows valid rows; import → DB shows Student.Program populated from the resolved section.
10. **AC9 ambiguous — legacy template, multi-program section** — seed a duplicate section name across two programs, upload legacy row referencing it → preview shows error "Section 'RUBY' exists in multiple programs for grade '11'. Add a 'Program' column to disambiguate."
11. **Empty system** — drop all sections, regenerate template, confirm Available Sections sheet shows the "(No active sections found…)" message instead of an empty table.
12. **Backwards-compat full path** — upload an old (pre-this-change) template that's been filled out for a single-program school → all rows valid, import succeeds.

---

## Risks & Considerations

- **Risk: Legacy template detection by header presence is brittle if a user manually deletes the Program header but leaves the column.** The detector relies on header text equality with "Program". If they delete the header, the validator drops to legacy mode and might silently accept rows whose data column is interpreted as ParentGuardianName. **Mitigation:** the legacy mode parser uses positional indices that match the legacy 11-column shape — a header-deleted-but-data-present file will produce wrong-column errors, not a silent data corruption (because `ParentGuardianName` etc. won't match values like "REGULAR"). Acceptable.
- **Risk: `GetAllSectionsAsync` may not eager-load `Program`.** Verified `GradeSectionService.cs:127-145` does include `Program`. If a future refactor removes the include, `ImportStudentsAsync` will throw or pick wrong section. Note in code review.
- **Risk: TempData JSON exceeds cookie size for large imports.** Existing concern, not introduced by this story — adding one nullable string field doesn't move the needle (~13 chars per row × 500 rows = ~6.5 KB extra). Flagged for follow-up if reports come in.
- **Risk: Performance — `GradeLevelPrograms` full-table scan per validate.** Tiny table (≤ 13 grades × ~10 programs); acceptable. Cached per validation call already.
- **Risk: Case sensitivity drift.** Program codes are stored in mixed case in seed data. The validator uppercases the input (`ToUpperInvariant()`) but compares against stored `p.Code` with `OrdinalIgnoreCase` — both are safe; the uppercase normalization on the row is for the generated error messages, not for matching. Tests cover lowercase input.
- **Risk: ClosedXML `XLCellValue.FromObject` quirks for empty strings vs. nulls.** Matches existing pattern in the codebase; no change.
- **Risk: Async migration of `GenerateStudentTemplate` could break a Razor binding I haven't grep'd for.** Phase 2 calls out a verify step. If anything else calls the sync version, fix at impl time.
- **Risk: No tests exist for the legacy validator path today** — first-time test coverage of this surface. May surface latent bugs during impl. Worth a separate small fix story if a real bug shows up; otherwise plan accommodates.

---

## Out of Scope

- Adding a unique DB index on `(GradeLevelId, ProgramId, Name)` — separate hardening story; this plan handles ambiguity at validation time.
- Faculty bulk import — Programs don't apply to Faculty.
- Annual batch re-enrollment — different code path; covered elsewhere.
- Localising the new error messages — English only, consistent with existing import.
- Excel data-validation dropdowns on the template (cell-level "list" validation) for Program and Section — would need named ranges referencing `Available Sections`; complexity not justified for v1.
- Streaming / progress UI for very large imports.
- A dry-run/preview-only mode separate from the existing two-step flow.
- Auto-creating missing Programs or Sections during import — explicitly rejected; admins must seed first.
- Cleanup of duplicated `AuthenticateDeviceAsync`-style helpers — unrelated.

---

## Estimated Effort

- Phase 1 (DTO + interface): ~10 min
- Phase 2 (template — header, samples, Available Sections population, Instructions, async migration): ~70 min
- Phase 3 (header detection + legacy parser): ~30 min
- Phase 4 (validator + import lookup parity): ~75 min
- Phase 5 (12 tests + helpers + manual verification): ~120 min
- Buffer for impl review & docs nits: ~15 min
- **Total:** ~5–6 hours

Aligns with the 5-pt estimate in US0120.

---

## Rollout Plan

1. Phase 1 — add `ProgramCode` to `StudentImportRow`. `dotnet build` clean.
2. Phase 2a — add Program column header & samples; rebuild template manually, eyeball.
3. Phase 2b — async refactor + Available Sections population. Update page handler. Rebuild, download template, eyeball.
4. Phase 2c — Instructions sheet refresh.
5. Phase 3 — header detection + legacy-mode column index map.
6. Phase 4a/b/c — validator pipeline + import lookup parity. `dotnet build` clean.
7. Phase 5 — write tests. `dotnet test` clean (existing 302 tests + new ones).
8. Manual verification (Phase 5 checklist 1–12).
9. Confirm with user before commit (project commit/push policy).
10. Commit on `dev` branch; PR to `main` (project git workflow).
11. Update epic EP0010 Story Breakdown table to include US0120 (currently stale — not blocking, optional cleanup).

**Implementation model recommendation:** switch to **Sonnet 4.6** for Phases 1–5 execution per project preference. Stay on Opus only for any plan revisions during impl.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-04 | Claude (Opus 4.7) | Initial plan drafted from US0120 |
