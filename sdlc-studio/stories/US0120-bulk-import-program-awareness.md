# US0120: Bulk Student Import — Program Awareness

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0040: Bulk Student Import — Program Awareness](../plans/PL0040-bulk-import-program-awareness.md)
> **Owner:** TBD
> **Created:** 2026-05-04
> **Marked Planned:** 2026-05-04
> **Marked Done:** 2026-05-05

## User Story

**As an** Admin Amy (registrar / school admin)
**I want** the bulk student import template and validator to be Program-aware
**So that** I can confidently import students into the correct Program-bound section without guessing, and the system rejects ambiguous or invalid program/section combinations instead of silently picking the wrong section.

## Context

### Persona Reference
**Admin Amy** — registrar performing batch student onboarding at the start of the school year via the existing `/Admin/BulkImportStudents` page.

### Background
EP0010 made `Program` a first-class concept: every graded `Section` is bound to a `Program` (`Section.ProgramId`), and `Student.Program` is denormalised from the section on enrollment. US0103 made `Section.ProgramId` nullable so Non-Graded sections can have no program at all.

The current bulk-import flow predates these changes. Three concrete gaps exist (verified 2026-05-04):

1. **Template has no Program column** (`BulkImportService.cs:439-444`). Importer relies on `(GradeLevel, Section name)` to infer the program.
2. **Section name is not unique within a grade level** (DB index in `ApplicationDbContext.cs:203` is non-unique). Lookup at `BulkImportService.cs:146-148` uses `FirstOrDefault` on `(GradeLevelId, Name)` — if two programs in the same grade have a section with the same name (e.g. Grade 11 STEM "RUBY" and Grade 11 ABM "RUBY"), the import silently picks whichever EF returns first.
3. **"Available Sections" reference sheet is empty.** `GenerateStudentTemplate` (`BulkImportService.cs:474-492`) creates the sheet but only writes a placeholder *"(This sheet is auto-populated when you download from the app with existing sections.)"* — nothing actually populates it. The Instructions sheet also makes no mention of Program or NG.

The end-state on disk is currently *correct* for unambiguous cases — `EnrollStudentAsync` at `GradeSectionService.cs:430` does set `student.Program = section.Program?.Code` from the resolved Section, and NG flows through with null. But the importer's user experience and disambiguation are broken.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0010 / US0060 | Domain | Graded sections have a mandatory Program | Template + validator must surface Program for graded grade levels |
| US0103 / US0106 | Domain | NG sections have no Program; `Student.Program` is null for NG | Program column must be optional and rejected (or ignored) for NG rows |
| TRD | Data | Section name not unique within a grade level | Importer must disambiguate by Program when more than one match exists |
| Existing UX | UI | Template is downloaded once and edited offline | Available-Sections reference must be populated at download time, not via a later round-trip |

---

## Acceptance Criteria

### AC1: Template Includes Program Column
- **Given** I download the student import template via "Download Template"
- **Then** the `Students` sheet contains a `Program` column positioned between `GradeLevel` and `Section`
- **And** the column header styling matches existing headers
- **And** the sample rows include realistic Program values (e.g. REGULAR, STEM, ABM) plus one NG row with a blank Program

### AC2: Available Sections Sheet Is Populated At Download
- **Given** I download the template
- **When** I open the `Available Sections` sheet
- **Then** it contains one row per active Section with columns: `GradeLevelCode | GradeLevelName | ProgramCode | ProgramName | SectionName`
- **And** NG sections appear with `ProgramCode` and `ProgramName` empty (not "REGULAR", not "—")
- **And** rows are sorted by `GradeLevel.SortOrder`, then `Program.SortOrder` (NG sorts to the end), then Section name
- **And** the placeholder/italic note is removed

### AC3: Instructions Sheet Documents Program & NG
- **Given** I open the `Instructions` sheet
- **Then** it lists `Program` with Required = "Conditional" and a description: *"Program code from 'Available Sections' sheet. Required for graded sections. Must be blank for Non-Graded (NG) rows."*
- **And** the `GradeLevel` description mentions that `NG` is allowed and means Non-Graded
- **And** the `Section` description no longer claims sections are unique by name; it directs the user to the `Available Sections` sheet

### AC4: Validator — Program Required for Graded Rows
- **Given** an import row with `GradeLevel ≠ NG`
- **And** the row's `Program` cell is blank
- **Then** the row fails validation with field=`Program`, message="Program is required for graded grade levels. See 'Available Sections' sheet."

### AC5: Validator — Program Forbidden for NG Rows
- **Given** an import row with `GradeLevel = NG`
- **And** the row's `Program` cell is non-empty
- **Then** the row fails validation with field=`Program`, message="Non-Graded rows must leave Program blank."

### AC6: Validator — Program Code Must Exist And Be Allowed For The Grade
- **Given** a graded row with `Program = "STEM"` and `GradeLevel = "7"`
- **And** there is no `GradeLevelProgram` row linking Grade 7 to STEM
- **Then** the row fails validation with field=`Program`, message="Program 'STEM' is not allowed for grade '7'."
- **And** an unknown program code yields message="Program 'XYZ' not found or inactive."

### AC7: Validator — Section Resolution Uses (GradeLevel, Program, Name)
- **Given** a graded row with `GradeLevel`, `Program`, and `Section` all filled
- **Then** the section lookup matches by `GradeLevelId` + `ProgramId` + `Name` (case-insensitive)
- **And** if no section matches, the row fails with field=`Section`, message="Section '{name}' not found under grade '{grade}' / program '{program}'."

### AC8: Validator — NG Section Resolution Uses (GradeLevel=NG, Name) With Null Program
- **Given** an NG row with blank `Program` and a `Section` value
- **Then** the section lookup matches sections where `GradeLevelId = NG_grade_id` and `ProgramId IS NULL` and `Name` matches
- **And** if no such section exists, the row fails with the same message form as AC7 (program shown as "—")

### AC9: Validator — Backward Compatibility For Templates Without Program Column
- **Given** an uploaded file whose `Students` sheet has only the legacy 11 columns (no `Program` column)
- **And** every section in the file is unambiguous within its grade level (exactly one program match)
- **Then** validation succeeds and the importer auto-resolves Program from the unique section
- **And** any row whose `(GradeLevel, Section)` resolves to **multiple** sections fails with field=`Program`, message="Section '{name}' exists in multiple programs for grade '{grade}'. Add a 'Program' column to disambiguate."

### AC10: Import Persistence Unchanged For The Happy Path
- **Given** valid rows are imported
- **Then** `Student.Program` is set from the resolved section's `Program.Code` (or null for NG) — no change to the existing enrollment path in `GradeSectionService.EnrollStudentAsync`
- **And** the audit log entry remains action=`BulkStudentImport` with the same details format

---

## Scope

### In Scope
- `BulkImportService.GenerateStudentTemplate` — add Program column, populate `Available Sections` sheet, refresh Instructions
- `BulkImportService.ValidateStudentXlsxAsync` — parse Program column, enforce AC4–AC9
- `StudentImportRow` — add `Program` field
- `BulkImportService.ImportStudentsAsync` — pass through resolved Section (no behavioural change since Section already carries Program)
- Tests in `tests/SmartLog.Web.Tests` covering AC4–AC9

### Out of Scope
- Faculty bulk import (separate template; Program does not apply to Faculty)
- Annual batch re-enrollment flow (uses a different code path, tracked elsewhere)
- Adding a unique DB index on `(GradeLevelId, ProgramId, Name)` — separate hardening story
- Retroactive correction of any historical imports (no production data exists pre-release)
- Localisation of new error messages — English only, consistent with existing import errors

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| User downloads new template, fills it in, uploads against an environment whose programs were edited mid-edit (program deactivated) | Validator reports "Program 'X' not found or inactive" per AC6 |
| Excel auto-formats the Program column as a number (e.g. ABM stays text but a code "11" might be coerced) | Read cell as string with ClosedXML (existing pattern); document in Instructions that Program is text |
| User imports an old template with the legacy 11-column layout against a school where every grade has exactly one program | All rows resolve unambiguously, import succeeds (AC9 happy path) |
| Same legacy template, but Grade 11 now has both STEM "RUBY" and ABM "RUBY" | Affected rows fail with the disambiguation message; rest of the file may still import |
| User puts `regular` (lowercase) in Program column | Case-insensitive match against `Program.Code`, succeeds |
| Row has `GradeLevel=NG` and `Program=REGULAR` | Fails AC5 — does not silently accept and overwrite |
| Row has `GradeLevel=NG`, `Program=` (blank), and a Section that doesn't exist under NG | Fails AC8 |

---

## Test Scenarios

- [ ] Generated template has 12 columns including `Program` between `GradeLevel` and `Section`
- [ ] `Available Sections` sheet is populated with a real row per active section, including NG rows with blank Program columns
- [ ] Instructions sheet describes Program with conditional-required semantics
- [ ] Valid import: graded row with correct Program + Section resolves and persists `Student.Program = "<code>"`
- [ ] Valid import: NG row with blank Program + valid NG section persists `Student.Program = null`
- [ ] Invalid: graded row missing Program → AC4 error
- [ ] Invalid: NG row with Program → AC5 error
- [ ] Invalid: unknown Program code → AC6 unknown-program error
- [ ] Invalid: Program not linked to that grade → AC6 not-allowed error
- [ ] Invalid: Section name doesn't exist under (Grade, Program) → AC7 error with both grade and program in the message
- [ ] Invalid: NG section name not found → AC8 error
- [ ] Legacy template (11 cols), unambiguous → succeeds (AC9 happy)
- [ ] Legacy template, ambiguous section → AC9 disambiguation error
- [ ] No regression: existing happy-path test for `ImportStudentsAsync` still passes (audit log + QR generation unchanged)

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Services/BulkImportService.cs` — template, validator, import row mapping
- **Modify:** `src/SmartLog.Web/Services/IBulkImportService.cs` — no signature change expected
- **Modify:** `src/SmartLog.Web/Validation/StudentImportRow.cs` (or wherever the row DTO lives) — add `Program` field
- **Modify:** `src/SmartLog.Web/Pages/Admin/BulkImportStudents.cshtml` — minor copy update for the Program column note (no flow change)
- **Add:** Tests under `tests/SmartLog.Web.Tests/` (likely a new `BulkImportProgramAwarenessTests.cs` alongside existing import tests)

### Implementation Notes
- Reuse `GradeSectionService.GetAllProgramsAsync` and `GetProgramsForGradeAsync` for AC6 validation; cache results once per validation call.
- `Available Sections` population needs `Sections` joined with `GradeLevel` and `Program` — load all in one EF query in `GenerateStudentTemplate`.
- For AC9 backward-compat detection: read header row of `Students` sheet; if `Program` header is missing, set a `legacyMode` flag on the validator and apply the disambiguation rule.
- Keep error messages aligned with existing tone — short, includes original value where helpful (`OriginalValue` on `ImportError`).

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0060](US0060-section-program-mandatory.md) | Predecessor | Section.ProgramId exists | Done |
| [US0103](US0103-section-programid-nullable.md) | Predecessor | NG section has null Program | Done |
| [US0106](US0106-student-program-null-for-ng.md) | Predecessor | Student.Program null for NG flows correctly | Done |

### Blocks
None — this is an isolated improvement to the import surface.

---

## Estimation

**Story Points:** 5
**Complexity:** Medium — surface area is one service + DTO + tests; logic is straightforward but has multiple branches (graded vs NG, legacy vs new template, ambiguity handling) that each need test coverage.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-05-04 | Claude | Initial draft from import-coverage review against EP0010 |
| 2026-05-05 | Claude | Implemented PL0040 (all phases). Also added Program column to Students list and removed Program from Grade & Section concatenation in StudentDetails (display cleanup, done alongside this story). |
