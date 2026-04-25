# PL0025: Student Details — Display Program Code with Grade & Section

> **Status:** Draft
> **Story:** [US0087: Student Details — Display Program Code with Grade & Section](../stories/US0087-student-details-program-code-display.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-25
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Add the Program Code to the Student Details page (header + info card), the Student list page (new sortable column), and student CSV/print exports. Program is read from the denormalised `Student.Program` field; falls back to `Student.Section.Program.Code` if null. No DB schema change.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Program Code in header | Details page header shows "Grade N — ProgramCode · Section: SectionName" |
| AC2 | Program row in info card | Info card row: "ProgramCode — ProgramName" |
| AC3 | REGULAR shown | REGULAR program renders correctly in header and card |
| AC4 | Student list Program column | List adds sortable/filterable Program column between Grade and Section |
| AC5 | Export includes Program | CSV/print export includes Program Code column |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
- **Architecture:** Razor Pages + EF Core 8.0
- **Test Framework:** xUnit + Moq

### Key Existing Patterns
- **Student.Program:** Denormalised `string? Program` field on the Student entity — set during Section assignment (EP0010). This is the fast-path value.
- **Fallback:** If `Student.Program` is null, resolve via `Student.Section?.Program?.Code` (requires Include in query).
- **Student Details page:** `Pages/Admin/Students/Details.cshtml(.cs)` — loads student with Includes; render header + info grid.
- **Student List page:** `Pages/Admin/Students/Index.cshtml(.cs)` — paginated table; filter/sort via query params.
- **Export service:** `Services/StudentExportService.cs` (or inline CSV helper in Index page model).

---

## Recommended Approach

**Strategy:** Test-After  
**Rationale:** Display-only change; no new DB columns. Tests cover Program fallback resolution and export column presence.

---

## Implementation Phases

### Phase 1: Details Page — Header + Info Card

**Goal:** Show Program in the student details page header and info card.

- [ ] In `Pages/Admin/Students/Details.cshtml.cs`, ensure the student query includes `Section.Program`:
  ```csharp
  _db.Students
      .Include(s => s.Section).ThenInclude(sec => sec.Program)
      .FirstOrDefaultAsync(s => s.Id == id)
  ```
- [ ] Add a helper property (or inline Razor expression) to resolve Program Code:
  ```csharp
  // In page model or Razor:
  var programCode = student.Program ?? student.Section?.Program?.Code ?? "—";
  var programName = student.Section?.Program?.Name ?? "";
  ```
- [ ] In `Details.cshtml` header block, update the grade/section display:
  ```html
  <span>Grade @student.GradeLevel — @programCode · Section: @student.SectionName</span>
  ```
- [ ] In the info card, add a row between Grade Level and Section:
  ```html
  <dt>Program</dt>
  <dd>
      @programCode
      @if (!string.IsNullOrEmpty(programName))
      {
          <span class="text-muted">— @programName</span>
      }
      @if (student.Section?.Program?.IsActive == false)
      {
          <span class="badge bg-warning ms-1">Inactive</span>
      }
  </dd>
  ```

**Files:** `Pages/Admin/Students/Details.cshtml(.cs)`

### Phase 2: Student List Page — Program Column

**Goal:** Add a Program column to the student list.

- [ ] In `Pages/Admin/Students/Index.cshtml.cs`, include `Section.Program` in the base query:
  ```csharp
  query = query.Include(s => s.Section).ThenInclude(sec => sec.Program);
  ```
- [ ] Project to a view model or include `ProgramCode` in the existing list DTO:
  ```csharp
  ProgramCode = s.Program ?? s.Section.Program.Code ?? "—"
  ```
- [ ] Add sort case for Program column (sort by `ProgramCode` ascending/descending).
- [ ] Add filter dropdown or text filter for Program (optional; filter by `ProgramCode`).
- [ ] In `Index.cshtml`, add `<th>` for Program between Grade and Section; `<td>@item.ProgramCode</td>` in the row.

**Files:** `Pages/Admin/Students/Index.cshtml(.cs)`

### Phase 3: Export — Program Column

**Goal:** Include Program Code in CSV and print exports.

- [ ] In `Services/StudentExportService.cs` (or wherever CSV headers/rows are built):
  - Add `"Program"` to the CSV header row.
  - Add `student.Program ?? student.Section?.Program?.Code ?? ""` to the data row.
  - Ensure the query that feeds the export includes `Section.Program`.
- [ ] If there is a print partial (`_StudentPrintRow.cshtml` or similar), add the Program field.

**Files:** `Services/StudentExportService.cs` (or inline export code)

### Phase 4: Tests

| AC | Test | File |
|----|------|------|
| AC1/AC2 | Details page model exposes correct ProgramCode (denormalised fast path) | `StudentDetailsTests.cs` |
| AC2 | Fallback to Section.Program.Code when Student.Program is null | same |
| AC3 | REGULAR program shows code and name correctly | same |
| AC5 | Export service includes Program column header and value | `StudentExportServiceTests.cs` |

- [ ] Run `dotnet test`; confirm zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | `Student.Program` null but Section has Program | Fall back to `Section.Program.Code` |
| 2 | Both null (legacy record, no Section) | Display "—" placeholder |
| 3 | Program is inactive | Show code + "Inactive" badge in info card |
| 4 | `Program = "REGULAR"` | Renders as "REGULAR — Regular" (or configured name) |

---

## Definition of Done

- [ ] Details page header shows `Grade N — ProgramCode · Section: SectionName`
- [ ] Details page info card has Program row with code + name
- [ ] Student list has a Program column (sortable)
- [ ] Fallback from denormalised field to Section.Program.Code works
- [ ] Inactive program shows badge
- [ ] Export includes Program column
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-25 | Claude | Initial plan |
