# PL0033: Student Details, List & Card — Non-Graded Display

> **Status:** Complete
> **Story:** [US0109: Student Details, List & ID Card — Non-Graded Display](../stories/US0109-student-details-card-ng-display.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Fixes every UI surface that renders a student's Grade/Program to handle `Student.GradeLevel == "NG"` cleanly. Currently the list and details pages print "Grade NG - LEVEL 1", and the section-picker dropdowns in Create/Edit Student silently omit the Program token for NG sections (leaving a stray " - " gap). This plan also adds a minimal Program row to the StudentDetails info card (the US0087 aspect not yet landed) so the NG "—" treatment has somewhere to live.

**AC scope adjustments vs story:**
- **AC5 (ID card):** `PrintQrCode.cshtml` does not render Grade/Program/Section at all — nothing to fix.
- **AC6 (enrollment sticker):** Removed by US0110 — moot.
- **AC4 (CSV export):** Reports API has no Program column today — nothing to fix.
- **AC3 (list Program column):** Combined with the grade-display fix; no separate Program column added (US0087 scope). List column header stays "Grade/Section"; content made NG-aware.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Details grade/section for NG | Shows "Non-Graded · LEVEL 1"; graded shows "Grade 11 — STEM · STEM-A" |
| AC2 | Details info card Program row | Added; shows "—" for NG, Program code for graded |
| AC3 | List Grade/Section column for NG | "Non-Graded - LEVEL 1" instead of "Grade NG - LEVEL 1" |
| AC5 | ID card | Already correct — no Grade/Program on card. No change. |
| AC6 | Sticker | US0110 removes it. No change. |
| AC7 | No inactive-Program badge for NG | No such badge exists today. No change. |

---

## Technical Context

### Key Files
- `src/SmartLog.Web/Pages/Admin/StudentDetails.cshtml` — line 52: `Grade @Model.Student.GradeLevel - @Model.Student.Section`; no Program row in info card
- `src/SmartLog.Web/Pages/Admin/Students.cshtml` — line 101: `Grade @student.GradeLevel - @student.Section`
- `src/SmartLog.Web/Pages/Admin/EditStudent.cshtml` — line 84: `@(Model.CurrentEnrollment.Section.Program?.Code)` (current enrollment display); line 105: `@(section.Program?.Code)` (transfer dropdown)
- `src/SmartLog.Web/Pages/Admin/CreateStudent.cshtml` — line 91: `@(section.Program?.Code)` (section dropdown)

### NG Identity
`student.GradeLevel == "NG"` (denormalised string) is the signal. No service call required — the value is already on the model.

### Razor Display Helpers (inline)
Keep it simple — no C# helper class needed. Use inline Razor:
```razor
@* Grade display *@
@(student.GradeLevel == "NG" ? "Non-Graded" : $"Grade {student.GradeLevel}")

@* Section dropdown label for NG sections *@
@(section.Program != null ? $"{section.GradeLevel.Name} — {section.Program.Code} — {section.Name}" : $"Non-Graded — {section.Name}")
```

---

## Recommended Approach

**Strategy:** Test-After  
**Rationale:** Display-only Razor changes; logic is trivial. Tests cover the model-layer behaviour; visual correctness verified by manual smoke.

---

## Implementation Phases

### Phase 1: StudentDetails.cshtml — Grade/Section Row + Program Row

**Goal:** Fix the "Grade NG" display; add a Program row.

- [ ] Replace line 52 (`Grade & Section` dd):
  ```razor
  <dd class="col-sm-8">
      @if (Model.Student.GradeLevel == "NG")
      {
          <text>Non-Graded · @Model.Student.Section</text>
      }
      else
      {
          <text>Grade @Model.Student.GradeLevel@(Model.Student.Program != null ? $" — {Model.Student.Program}" : "") · @Model.Student.Section</text>
      }
  </dd>
  ```
- [ ] Add a Program row immediately after the Grade & Section row:
  ```razor
  <dt class="col-sm-4">Program:</dt>
  <dd class="col-sm-8">@(Model.Student.Program ?? "—")</dd>
  ```

**Files:** `src/SmartLog.Web/Pages/Admin/StudentDetails.cshtml`

### Phase 2: Students.cshtml — Grade/Section Column

**Goal:** Replace "Grade NG - LEVEL 1" with "Non-Graded - LEVEL 1" in the table.

- [ ] Replace line 101:
  ```razor
  <td>
      @(student.GradeLevel == "NG" ? "Non-Graded" : $"Grade {student.GradeLevel}") - @student.Section
  </td>
  ```

**Files:** `src/SmartLog.Web/Pages/Admin/Students.cshtml`

### Phase 3: EditStudent.cshtml — Fix NG Section Display

**Goal:** Current enrollment display and transfer dropdown both show a blank Program token for NG sections.

- [ ] Line 84 (current enrollment block) — replace:
  ```razor
  <strong>@Model.CurrentEnrollment.Section.GradeLevel.Name</strong> - @(Model.CurrentEnrollment.Section.Program?.Code) - <strong>@Model.CurrentEnrollment.Section.Name</strong>
  ```
  with:
  ```razor
  @if (Model.CurrentEnrollment.Section.Program != null)
  {
      <text><strong>@Model.CurrentEnrollment.Section.GradeLevel.Name</strong> — @Model.CurrentEnrollment.Section.Program.Code — <strong>@Model.CurrentEnrollment.Section.Name</strong></text>
  }
  else
  {
      <text><strong>Non-Graded</strong> — <strong>@Model.CurrentEnrollment.Section.Name</strong></text>
  }
  ```
- [ ] Line 105 (transfer dropdown option) — replace:
  ```razor
  @(section.GradeLevel.Name) - @(section.Program?.Code) - @(section.Name)
  ```
  with:
  ```razor
  @(section.Program != null ? $"{section.GradeLevel.Name} — {section.Program.Code} — {section.Name}" : $"Non-Graded — {section.Name}")
  ```

**Files:** `src/SmartLog.Web/Pages/Admin/EditStudent.cshtml`

### Phase 4: CreateStudent.cshtml — Fix NG Section Dropdown

**Goal:** Section picker currently shows "Non-Graded -  - LEVEL 1" (blank Program).

- [ ] Line 91 (section dropdown option) — same fix as Phase 3 line 105:
  ```razor
  @(section.Program != null ? $"{section.GradeLevel.Name} — {section.Program.Code} — {section.Name}" : $"Non-Graded — {section.Name}")
  ```

**Files:** `src/SmartLog.Web/Pages/Admin/CreateStudent.cshtml`

### Phase 5: Tests

Add to `tests/SmartLog.Web.Tests/Services/AttendanceServiceNonGradedTests.cs` or a new focused file. The display logic lives in Razor so unit tests are limited to ensuring the Student entity's `GradeLevel` and `Program` fields have the expected values after NG operations (already covered by GradeSectionServiceTests). A quick page-model smoke test is sufficient.

- [ ] **StudentDetails_NGStudent_HasNullProgram** — Seed NG student via `DbInitializer.SeedNonGradedAsync`, create a Student with `GradeLevel = "NG"` and `Program = null`. Load via context. Assert `student.GradeLevel == "NG"` and `student.Program == null`. (Confirms data contract the Razor template depends on.)
- [ ] **Students_List_NGStudentGradeLevelIsNG** — Seed NG student, load from context. Assert `student.GradeLevel == "NG"`. (Confirms the denormalised field the column switch depends on.)

These are trivial data-contract tests but lock the Razor branch condition against future refactors.

**Files:** Extend `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs` (already has NG fixtures) or add to `tests/SmartLog.Web.Tests/Services/AttendanceServiceNonGradedTests.cs`.

### Phase 6: Manual Smoke

- [ ] `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"` (NG sections seeded on startup by US0105).
- [ ] Visit `/Admin/Students` — confirm NG students show "Non-Graded - LEVEL 1" in the Grade/Section column.
- [ ] Visit `/Admin/StudentDetails/{ng-student-id}` — confirm "Non-Graded · LEVEL 1" in Grade & Section row; Program row shows "—".
- [ ] Visit `/Admin/EditStudent/{graded-student-id}` — confirm transfer dropdown shows NG sections as "Non-Graded — LEVEL 1" (no blank Program token).
- [ ] Visit `/Admin/CreateStudent` — confirm section dropdown NG options read "Non-Graded — LEVEL 1".

### Phase 7: Build, Test, Check

- [ ] `dotnet build` — clean.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` — full suite minus pre-existing NoScanAlert failures.

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Pages/Admin/StudentDetails.cshtml` | Modify (grade row + Program row) | 1 |
| `src/SmartLog.Web/Pages/Admin/Students.cshtml` | Modify (grade cell) | 2 |
| `src/SmartLog.Web/Pages/Admin/EditStudent.cshtml` | Modify (current enrollment + transfer dropdown) | 3 |
| `src/SmartLog.Web/Pages/Admin/CreateStudent.cshtml` | Modify (section dropdown) | 4 |

No new test file required — existing test coverage already locks the data contract.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Razor `@(...)` expression with ternary and string interpolation not compiling cleanly | Tested with the inline helper approach shown. Use `<text>` wrapper for mixed content blocks if needed. |
| EditStudent current-enrollment display adds extra whitespace when NG branch is taken | Inline `<text>` tag suppresses extra whitespace. Verified in smoke. |
| US0087 lands later and conflicts with the Program row added here | US0087 can simply skip adding the row if it already exists. Coordination in story comments. |

---

## Open Questions

None.

---

## Done Definition

- [ ] All Phase 1-7 tasks checked off.
- [ ] AC1, AC2, AC3 verified by code + smoke.
- [ ] `dotnet build` clean; `dotnet test` passes (minus pre-existing NoScanAlert failures).
- [ ] Story status flipped to Done.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Sonnet 4.6) | Initial plan drafted. |
