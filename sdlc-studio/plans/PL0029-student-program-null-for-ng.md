# PL0029: Student.Program Denormalisation — Null for Non-Graded Enrollments

> **Status:** Complete
> **Story:** [US0106: Student.Program Denormalisation — Null for Non-Graded Enrollments](../stories/US0106-student-program-null-for-ng.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8.0

## Overview

Most of US0106 is already implemented in disguise: `GradeSectionService.EnrollStudentAsync` (line 430) and `TransferStudentAsync` (line 486) already use `section.Program?.Code` — null-safe, so NG sections (with `ProgramId = null`) naturally produce `Student.Program = null`. `Student.Program` is already declared `string?` (Student.cs:53). No entity, schema, or service-method-signature change is required for those two paths.

The real gap is **`BatchReenrollmentService.PromoteAsync`** (line 270, 360-364): it preloads sections **without** including `Section.Program` and never updates `Student.Program` on promotion at all. That's a pre-existing bug visible from US0064 — it just becomes more obvious with NG in the mix. This plan fixes that and adds NG-aware test coverage that locks the contract in for all three paths (Enroll, Transfer, Batch).

---

## Acceptance Criteria Summary

| AC | Name | Description | Already Done? |
|----|------|-------------|---------------|
| AC1 | New NG enrollment → Program null | `EnrollStudentAsync` to NG section sets `Student.Program = null` | Yes (verify with test) |
| AC2 | New graded enrollment → Program code | `EnrollStudentAsync` to graded sets `Student.Program = "STEM"` etc. | Yes (verify with test) |
| AC3 | Move graded → NG | `TransferStudentAsync` to NG nulls `Student.Program` | Yes (verify with test) |
| AC4 | Move NG → graded | `TransferStudentAsync` to graded sets `Student.Program` | Yes (verify with test) |
| AC5 | Bulk re-enrol honors NG | `BatchReenrollmentService` promotion path sets `Student.Program` correctly (null for NG, code for graded) | **No — bug fix** |
| AC6 | `Student.Program` is `string?` | Entity field nullable | Yes (already correct) |

---

## Technical Context

### Language & Framework
- C# 12 / ASP.NET Core 8.0 / EF Core 8.0
- xUnit + EF Sqlite-in-memory via `TestDbContextFactory`

### Key Existing Files (current state)
- `src/SmartLog.Web/Data/Entities/Student.cs:53` — `public string? Program { get; set; }` ✓
- `src/SmartLog.Web/Services/GradeSectionService.cs:430` — `student.Program = section.Program?.Code;` ✓
- `src/SmartLog.Web/Services/GradeSectionService.cs:486` — `student.Program = newSection.Program?.Code;` ✓
- `src/SmartLog.Web/Services/BatchReenrollmentService.cs:270-273` — Sections preload **lacks** `.Include(s => s.Program)`
- `src/SmartLog.Web/Services/BatchReenrollmentService.cs:360-364` — Promotion update sets `GradeLevel`, `Section`, `UpdatedAt` but **not `Program`**

### NG Behaviour (today)
- `Section.ProgramId = null` for NG sections (US0103+US0105 landed).
- `Section.Program` navigation is null when ProgramId is null.
- `section.Program?.Code` short-circuits to `null` — desired.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Tiny code change (one Include + one assignment) bundled with comprehensive integration tests. TDD here would invert the natural workflow given how minimal the production code change is.

---

## Implementation Phases

### Phase 1: Fix BatchReenrollmentService — Include Program

**Goal:** Load `Section.Program` so the promotion path can read `section.Program?.Code`.

- [ ] In `src/SmartLog.Web/Services/BatchReenrollmentService.cs` line 270, add the Include:
  ```csharp
  var sections = await _context.Sections
      .Include(s => s.GradeLevel)
      .Include(s => s.Program)
      .Where(s => sectionIds.Contains(s.Id))
      .ToDictionaryAsync(s => s.Id);
  ```

**Files:** `src/SmartLog.Web/Services/BatchReenrollmentService.cs`

### Phase 2: Fix BatchReenrollmentService — Set Student.Program on Promotion

**Goal:** Update `Student.Program` denormalisation in the bulk promotion path so it stays consistent with single-student `EnrollStudentAsync`.

- [ ] In `src/SmartLog.Web/Services/BatchReenrollmentService.cs` around line 360-364, add the Program assignment alongside the existing GradeLevel/Section assignments:
  ```csharp
  // Update student denormalized fields
  student.CurrentEnrollmentId = enrollment.Id;
  student.GradeLevel = section.GradeLevel.Code;
  student.Section = section.Name;
  student.Program = section.Program?.Code; // null for Non-Graded sections (US0106)
  student.UpdatedAt = DateTime.UtcNow;
  ```

**Files:** `src/SmartLog.Web/Services/BatchReenrollmentService.cs`

### Phase 3: Verify — No Other Update Sites Forgot Student.Program

**Goal:** Confirm no other service mutates Student denormalised fields without also touching Program.

- [ ] Run:
  ```bash
  grep -rn "student\.GradeLevel\s*=\|student\.Section\s*=" src/SmartLog.Web
  ```
- [ ] For each hit, confirm the same code block also assigns `student.Program`. If any block updates Section but not Program, file as a follow-up bug — out of scope for this story unless trivial.

**Files:** none modified (audit only).

### Phase 4: Tests — Single-Student Enroll + Transfer (Lock Existing Behaviour)

**Goal:** Capture the AC1–AC4 contract in tests so future refactors can't regress NG handling.

Add to `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs`:

- [ ] **EnrollStudentAsync_GradedSection_SetsProgramCode** — Enroll into Grade 7 + REGULAR, assert `Student.Program == "REGULAR"`.
- [ ] **EnrollStudentAsync_NonGradedSection_SetsProgramNull** — Seed NG via `DbInitializer.SeedNonGradedAsync`, enroll into LEVEL 1, assert `Student.Program == null`.
- [ ] **TransferStudentAsync_GradedToNG_NullsProgram** — Enroll in Grade 7 REGULAR (Program="REGULAR"), then transfer to NG LEVEL 1. Assert `Student.Program == null`.
- [ ] **TransferStudentAsync_NGToGraded_SetsProgramCode** — Enroll in NG LEVEL 1 (Program=null), then transfer to Grade 7 REGULAR. Assert `Student.Program == "REGULAR"`.

**Files:** `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs`

### Phase 5: Test — BatchReenrollmentService Honors NG

**Goal:** Cover AC5 — the bulk path now correctly sets Program for both NG and graded promotions.

The existing `BatchReenrollmentServiceTests` file is the right home. Add:

- [ ] **PromoteAsync_StudentToNGSection_SetsProgramNull** — Pre-state: student in Grade 6 with `Program = "REGULAR"`. Promote to NG LEVEL 1. Assert `Student.Program == null`, `Student.GradeLevel == "NG"`, `Student.Section == "LEVEL 1"`, `Student.CurrentEnrollmentId` updated.
- [ ] **PromoteAsync_StudentToGradedSection_SetsProgramCode** — Pre-state: student in Grade 7 NG section with `Program = null`. Promote to Grade 8 REGULAR. Assert `Student.Program == "REGULAR"`.

> **Note:** the existing `BatchReenrollmentServiceTests` file may not have an NG-aware fixture. If its existing helpers tightly couple to graded-only seed data, write a small ad-hoc seed within the new tests rather than expanding shared helpers.

**Files:** `tests/SmartLog.Web.Tests/Services/BatchReenrollmentServiceTests.cs`

### Phase 6: Build, Test, Smoke

- [ ] `dotnet build` — clean.
- [ ] `dotnet test --filter "FullyQualifiedName~GradeSectionServiceTests|FullyQualifiedName~BatchReenrollmentServiceTests"` — green.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` — full suite (excluding pre-existing NoScanAlert failures).

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Services/BatchReenrollmentService.cs` | Modify (Include + Program assignment) | 1, 2 |
| `tests/SmartLog.Web.Tests/Services/GradeSectionServiceTests.cs` | Modify (4 new tests) | 4 |
| `tests/SmartLog.Web.Tests/Services/BatchReenrollmentServiceTests.cs` | Modify (2 new tests) | 5 |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Existing `BatchReenrollmentServiceTests` rely on a harness that doesn't include the NG seed | Use `DbInitializer.SeedNonGradedAsync` (now public, US0105) directly in the new tests — same pattern used by `SectionPagesNonGradedTests`. |
| `BatchReenrollmentService` graduation path also needs Program null'd | The graduation path doesn't create a new enrollment (student leaves school). Out of scope. |
| Other services (e.g., a hypothetical CSV bulk re-import) duplicate enrollment logic | Phase 3 audit catches this. None expected. |
| Prior `student.Program` value persists if the old code path skipped the assignment | New Phase 2 line ensures fresh assignment on every promotion. The `?.Code` propagation handles both directions. |

---

## Open Questions

None.

---

## Done Definition

- [ ] All Phase 1-6 tasks checked off.
- [ ] All AC1-AC6 covered by code + test evidence.
- [ ] Build clean; new tests + full suite (minus NoScanAlert) green.
- [ ] Story status flipped to Review by `code verify`.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Opus 4.7) | Initial plan drafted. |
