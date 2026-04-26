# PL0026: Section.ProgramId Nullable — Allow Sections Without Program (NG)

> **Status:** Complete
> **Story:** [US0103: Section.ProgramId Nullable — Allow Sections Without Program (NG)](../stories/US0103-section-programid-nullable.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

Foundation story for Non-Graded learners (no Program). Today `Section.ProgramId` is `Guid` (NOT NULL) with a Required FK to `Programs`. This plan relaxes the column to nullable, updates the EF configuration, generates a migration, and adds an app-level validation rule: ProgramId is **required for graded grade levels** and **forbidden when GradeLevel.Code = "NG"**. No production data exists (pre-release), so no data preservation work is required — US0105 will null out any dev-DB legacy NG→REGULAR links.

This is a foundation-only change; UI conditional render (US0104), seed (US0105), enrollment denormalisation (US0106), and downstream surfaces are tracked in their own stories.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Column nullable | Migration drops NOT NULL on `Sections.ProgramId`; FK preserved |
| AC2 | Entity nullable | `Section.ProgramId` is `Guid?`; `Program` nav is `Program?` |
| AC3 | ProgramId required for non-NG | Saving graded section without ProgramId returns model error "Program is required for graded sections" |
| AC4 | ProgramId forbidden for NG | Saving NG section with a ProgramId returns model error "Non-Graded sections must not have a Program" |
| AC5 | EF FK permits NULL | `OnModelCreating` Section→Program relationship configured `IsRequired(false)`, `OnDelete(Restrict)` retained |
| AC6 | No data loss | Pre-release: cleanup deferred to US0105 seed; this plan does not touch existing rows |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / ASP.NET Core 8.0 Razor Pages + EF Core 8.0
- **Test Framework:** xUnit + Moq + EF InMemory / SQLite InMemory
- **Migration tool:** `dotnet ef migrations`

### Key Existing Files
- `src/SmartLog.Web/Data/Entities/Section.cs` — entity (ProgramId: `Guid`, line 42; Program nav: `Program` not-null, line 47)
- `src/SmartLog.Web/Data/ApplicationDbContext.cs` — Section FK config at lines 209-217 (GradeLevel) and 425-429 (Program)
- `src/SmartLog.Web/Services/GradeSectionService.cs` — `CreateSectionAsync(gradeLevelId, name, programId, ...)` line 166; `UpdateSectionAsync(section)` line 217
- `src/SmartLog.Web/Services/IGradeSectionService.cs` — interface line 25
- `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml.cs` — `InputModel.ProgramId` is `Guid` `[Required]` line 47-48
- `src/SmartLog.Web/Pages/Admin/EditSection.cshtml.cs` — `InputModel.ProgramId` is `Guid` `[Required]` line 46-47

### Key Existing Patterns
- Page handler pattern: `[BindProperty] InputModel`, `OnPostAsync` validates `ModelState.IsValid` then delegates to service.
- Service throws `InvalidOperationException` for FK lookup failures; page catches and adds model error.
- Migration naming: `YYYYMMDDHHMMSS_DescriptiveName.cs` — most recent: `20260425061055_AddCalendarEventSuppressesNoScanAlert`.

### NG Identification
GradeLevel `Code = "NG"` is the sentinel. Validation must look up the GradeLevel by ID and check Code. Avoid hard-coding the GradeLevel Id; resolve via Code at validation time so seed-order doesn't matter.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Schema-level change with a small, well-defined validation rule. Tests cover the four state combinations (graded+programId, graded+null, NG+programId, NG+null) plus the migration round-trip. TDD gives little extra here because the implementation surface is mechanical.

---

## Implementation Phases

### Phase 1: Entity Model — Make ProgramId Nullable

**Goal:** Update the C# model to reflect the new schema contract.

- [ ] In `src/SmartLog.Web/Data/Entities/Section.cs`:
  ```csharp
  /// <summary>
  /// FK to Program. Required for graded grade levels; null for Non-Graded (US0103).
  /// </summary>
  public Guid? ProgramId { get; set; }

  // Navigation properties
  public virtual GradeLevel GradeLevel { get; set; } = null!;
  public virtual Faculty? Adviser { get; set; }
  public virtual Program? Program { get; set; }
  ```
- [ ] Remove the existing `[Required]`-style summary line about US0060; replace with the US0103 note above.

**Files:** `src/SmartLog.Web/Data/Entities/Section.cs`

### Phase 2: DbContext FK Configuration

**Goal:** Configure EF to allow null FK with restrict-on-delete preserved.

- [ ] In `src/SmartLog.Web/Data/ApplicationDbContext.cs` at the existing Section→Program block (lines 425-429), update to explicit optional:
  ```csharp
  builder.Entity<Section>()
      .HasOne(e => e.Program)
      .WithMany(p => p.Sections)
      .HasForeignKey(e => e.ProgramId)
      .IsRequired(false)
      .OnDelete(DeleteBehavior.Restrict);
  ```
- [ ] Verify no other place in `OnModelCreating` declares `Section.ProgramId` as required.

**Files:** `src/SmartLog.Web/Data/ApplicationDbContext.cs`

### Phase 3: Generate & Apply Migration

**Goal:** Schema column change.

- [ ] Run from repo root:
  ```bash
  dotnet ef migrations add SectionProgramIdNullable -p src/SmartLog.Web
  ```
- [ ] Inspect the generated migration. Expected `Up` direction:
  ```csharp
  migrationBuilder.AlterColumn<Guid>(
      name: "ProgramId",
      table: "Sections",
      type: "uniqueidentifier",
      nullable: true,
      oldClrType: typeof(Guid),
      oldType: "uniqueidentifier");
  ```
- [ ] Verify `Down` re-applies NOT NULL (acceptable since pre-release; consumers of the down path should null-fix data first — comment this expectation in the migration if EF's auto-generation isn't clear).
- [ ] Apply locally: `dotnet ef database update -p src/SmartLog.Web` and confirm no schema errors.
- [ ] Confirm `ApplicationDbContextModelSnapshot.cs` was updated automatically.

**Files:** `src/SmartLog.Web/Migrations/{timestamp}_SectionProgramIdNullable.cs(.Designer.cs)`, `ApplicationDbContextModelSnapshot.cs`

### Phase 4: Service Signature & Validation

**Goal:** Update `GradeSectionService` to enforce the new rule centrally.

- [ ] In `src/SmartLog.Web/Services/IGradeSectionService.cs` change line 25:
  ```csharp
  Task<Section> CreateSectionAsync(Guid gradeLevelId, string name, Guid? programId, Guid? adviserId = null, int capacity = 40);
  ```
- [ ] In `src/SmartLog.Web/Services/GradeSectionService.cs` `CreateSectionAsync`:
  - Change `programId` param to `Guid? programId`.
  - Replace the unconditional `Programs.FindAsync(programId)` block with the new branching rule:
    ```csharp
    var isNonGraded = string.Equals(gradeLevel.Code, "NG", StringComparison.OrdinalIgnoreCase);

    if (isNonGraded)
    {
        if (programId.HasValue)
            throw new InvalidOperationException("Non-Graded sections must not have a Program.");
    }
    else
    {
        if (!programId.HasValue)
            throw new InvalidOperationException("Program is required for graded sections.");
        var program = await _context.Programs.FindAsync(programId.Value);
        if (program == null)
            throw new InvalidOperationException($"Program with ID {programId.Value} not found.");
    }
    ```
  - Update Section construction: `ProgramId = programId,`
  - Update log line to handle null Program: `_logger.LogInformation("Created section: {Grade} - {Program} - {Section} (ID: {Id})", gradeLevel.Name, programId?.ToString() ?? "(none)", name, section.Id);` (or look up program code only when set).
- [ ] In `UpdateSectionAsync`, add the same NG vs graded validation **before** persisting. Re-read the section's GradeLevel from DB (caller may pass an unattached entity):
  ```csharp
  var gl = await _context.GradeLevels.FindAsync(section.GradeLevelId);
  var isNonGraded = string.Equals(gl?.Code, "NG", StringComparison.OrdinalIgnoreCase);
  if (isNonGraded && section.ProgramId.HasValue)
      throw new InvalidOperationException("Non-Graded sections must not have a Program.");
  if (!isNonGraded && !section.ProgramId.HasValue)
      throw new InvalidOperationException("Program is required for graded sections.");
  ```

**Files:** `src/SmartLog.Web/Services/IGradeSectionService.cs`, `src/SmartLog.Web/Services/GradeSectionService.cs`

### Phase 5: Page Handlers — Bind Nullable

**Goal:** Allow the binders to receive null and surface service-level validation errors against the right field.

- [ ] `Pages/Admin/CreateSection.cshtml.cs`:
  - `InputModel.ProgramId` → `public Guid? ProgramId { get; set; }`
  - Remove `[Required]` attribute on `ProgramId`. Keep `[Display(Name = "Program")]`.
  - In the `try` block, wrap the call in a try/catch that maps `InvalidOperationException` from the service to `ModelState.AddModelError(nameof(Input.ProgramId), ex.Message);` and re-renders.
- [ ] `Pages/Admin/EditSection.cshtml.cs`:
  - Same `InputModel.ProgramId` → `Guid?` change, remove `[Required]`.
  - On `OnPostAsync`, after fetching `section`, set `section.ProgramId = Input.ProgramId;` (already done; just confirm null path works).
  - Add the same `InvalidOperationException` → ModelState mapping.
- [ ] No JS / cshtml changes here — UI conditional render is **US0104**'s scope. This story leaves the form rendering Program as still-shown; tampering tests still rely on server-side validation.

**Files:** `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml.cs`, `src/SmartLog.Web/Pages/Admin/EditSection.cshtml.cs`

### Phase 6: Verify No Other Callers Broke

**Goal:** Confirm we didn't miss a caller of `CreateSectionAsync(...)` that passed a positional `Guid` for `programId`.

- [ ] Grep:
  ```bash
  grep -rn "CreateSectionAsync(" src/SmartLog.Web tests/
  ```
- [ ] Any caller passing a non-nullable `Guid` will still compile (implicit conversion to `Guid?`). Confirm test factories and seed code still compile.
- [ ] Build the solution: `dotnet build`. Address any `CS86xx` nullability warnings/errors that surface from the entity change (Section.Program nav becoming nullable).

**Files:** various — patch only as needed.

### Phase 7: Tests

**Goal:** Cover the four state combinations + migration smoke.

Add to `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs` (create file if absent — match other service-test layout in that project):

- [ ] **CreateSectionAsync_GradedWithProgram_Succeeds** — seed Grade 7 + Program REGULAR; create section with ProgramId; assert saved with ProgramId set.
- [ ] **CreateSectionAsync_GradedWithoutProgram_Throws** — seed Grade 7 only; call with `programId: null`; assert `InvalidOperationException` with message containing "Program is required".
- [ ] **CreateSectionAsync_NonGradedWithProgram_Throws** — seed NG GradeLevel + Program REGULAR; call with ProgramId; assert exception "must not have a Program".
- [ ] **CreateSectionAsync_NonGradedWithoutProgram_Succeeds** — seed NG; call with `programId: null`; assert saved with `ProgramId == null`.
- [ ] **UpdateSectionAsync_NonGradedWithProgram_Throws** — load existing NG section, set ProgramId, call Update; assert exception.
- [ ] **UpdateSectionAsync_GradedClearsProgram_Throws** — load Grade 7 section, set ProgramId = null, call Update; assert exception.

Page-handler integration tests (light, optional but recommended given Razor binding edge case):
- [ ] **CreateSectionPage_PostNullProgramForGraded_RedisplaysWithModelError** — POST InputModel with `GradeLevelId = Grade7.Id`, `ProgramId = null`; assert PageResult with ModelState error keyed to `Input.ProgramId`.
- [ ] **CreateSectionPage_PostNullProgramForNG_Succeeds** — POST with `GradeLevelId = NG.Id`, `ProgramId = null`; assert RedirectToPage("/Admin/Sections").

Migration round-trip (manual smoke, no automated test):
- [ ] On a clean DB, `dotnet ef database update`, then `dotnet ef migrations remove`, ensure no errors. Re-add and re-apply. (Don't commit the remove; this is a sanity check.)

**Files:** `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs`, possibly `tests/SmartLog.Web.Tests/Pages/Admin/CreateSectionPageTests.cs`

### Phase 8: Build, Test, Check

- [ ] `dotnet build` — clean.
- [ ] `dotnet test` — all green; new tests pass; existing tests unaffected.
- [ ] Quick manual smoke: launch the app (`dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"`), open Create Section, submit a Grade 7 section without selecting a Program (use browser DevTools to remove the `required` attribute) → expect a server-side validation error against the Program field. (This proves the server enforces the rule even when UI changes haven't landed yet.)

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Data/Entities/Section.cs` | Modify | 1 |
| `src/SmartLog.Web/Data/ApplicationDbContext.cs` | Modify | 2 |
| `src/SmartLog.Web/Migrations/{ts}_SectionProgramIdNullable.cs` | Create (via EF) | 3 |
| `src/SmartLog.Web/Migrations/{ts}_SectionProgramIdNullable.Designer.cs` | Create (via EF) | 3 |
| `src/SmartLog.Web/Migrations/ApplicationDbContextModelSnapshot.cs` | Modify (via EF) | 3 |
| `src/SmartLog.Web/Services/IGradeSectionService.cs` | Modify | 4 |
| `src/SmartLog.Web/Services/GradeSectionService.cs` | Modify | 4 |
| `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml.cs` | Modify | 5 |
| `src/SmartLog.Web/Pages/Admin/EditSection.cshtml.cs` | Modify | 5 |
| `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs` | Modify or Create | 7 |
| `tests/SmartLog.Web.Tests/Pages/Admin/CreateSectionPageTests.cs` | Create (optional) | 7 |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Hidden caller of `CreateSectionAsync` in seed/test code passes `Guid.Empty` instead of null and slips past validation | Phase 6 grep + service-level guard rejects `Guid.Empty` if encountered (treat as "not provided"). Add: `if (programId == Guid.Empty) programId = null;` at top of method. |
| EF auto-generated `Down` migration fails on a DB with NULL ProgramId rows | Pre-release; not a real concern. Document in migration comment that `Down` requires non-null backfill if used. |
| `Section.Program` nullability change cascades C# nullable warnings into Includes / projection sites | Address with `?.` in projections; expected to be a small surface. Resolve during Phase 6 build. |
| Service throws `InvalidOperationException` but page handler currently catches generic `Exception` and surfaces "An error occurred" to user — unhelpful | Add a specific `catch (InvalidOperationException ex)` clause **before** the generic catch, mapping to ModelState as described in Phase 5. |

---

## Open Questions

None. All four stakeholder questions answered 2026-04-26 (no production data, NG peer to Programs in broadcast UI, exclude NG from program filter, bulk import accepts NG).

---

## Done Definition

- [ ] All Phase 1-8 tasks checked off.
- [ ] All AC1-AC6 covered by code + test evidence.
- [ ] `dotnet build` clean, `dotnet test` green.
- [ ] Manual smoke confirms server-side validation rejects tampered submission.
- [ ] Story status flipped to Review by `code verify`.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Opus 4.7) | Initial plan drafted. |
