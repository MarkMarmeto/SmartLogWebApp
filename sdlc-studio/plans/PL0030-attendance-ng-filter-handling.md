# PL0030: Attendance — Non-Graded Filter Handling

> **Status:** Complete
> **Story:** [US0108: Attendance — Non-Graded Filter Handling](../stories/US0108-attendance-reports-ng-handling.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8.0

## Overview

Story US0108 was trimmed on 2026-04-26 (see story Revision History) after inspection revealed that Reports have no Program column or filter today, and Dashboard `attendance-by-grade` has no `?program=` parameter. The descoped work is now flagged for a future US0110.

This narrow plan locks in the AttendanceApi NG-filter contract with tests. **No production code change is expected** — the existing `AttendanceService` filters (`s.Program == programFilter`, `s.GradeLevel == gradeFilter`) already produce the right behaviour for NG students because:
- `Student.Program == null` for NG students (US0106)
- SQL `WHERE Program = 'X'` excludes NULL — NG students naturally drop out of program-filtered queries
- `Student.GradeLevel == "NG"` for NG students — `?grade=NG` matches them

The plan validates this with integration tests against the in-memory context.

---

## Acceptance Criteria Summary

| AC | Name | Description | Production Change? |
|----|------|-------------|---------------------|
| AC1 | `?program=` filter excludes NG | `?program=REGULAR` returns 0 NG students | None — verify only |
| AC2 | No filter includes NG | No `?program=` → NG students appear | None — verify only |
| AC3 | `?grade=NG` works | `?grade=NG` returns only NG students | None — verify only |
| AC4 | Combined `?program=...&grade=NG` returns empty | AND-of-filters correctly excludes everything | None — verify only |
| AC5 | Audit-logs unchanged | No-op | None |

---

## Technical Context

### Language & Framework
- C# 12 / ASP.NET Core 8.0 / EF Core 8.0
- xUnit + EF Sqlite-in-memory via `TestDbContextFactory`

### Key Existing Files (current state, no changes needed)
- `src/SmartLog.Web/Controllers/Api/AttendanceApiController.cs` — accepts `?program=`, `?grade=`, `?section=`, etc.
- `src/SmartLog.Web/Services/AttendanceService.cs:48-50` — `if (programFilter) where s.Program == programFilter` (line 50)
- `src/SmartLog.Web/Services/AttendanceService.cs:38-41` — `if (gradeFilter) where s.GradeLevel == gradeFilter` (line 40)
- Same filter pattern repeated at `:138-145, :157-158, :187-203, :228` for list/count/detail variants

### NG Behaviour (already correct)
- NG students have `Student.Program == null` (after PL0029) — `WHERE Program = 'STEM'` excludes them by SQL semantics.
- NG students have `Student.GradeLevel == "NG"` (after PL0027 seed + PL0029 enrollment) — `WHERE GradeLevel = 'NG'` matches them.

---

## Recommended Approach

**Strategy:** Test-After (with a twist — there's no After here; the production code already works).
**Rationale:** The contract is already correct. Tests are the deliverable.

---

## Implementation Phases

### Phase 1: Verify Service Layer — No Code Change

**Goal:** Confirm the existing filter logic at `AttendanceService.cs:48-50, 157-158, 202-203` covers all four ACs. No edits expected.

- [ ] Re-read `AttendanceService.GetAttendanceSummaryAsync`, `GetAttendanceListAsync`, `GetAttendanceCountAsync`, and `BuildAttendanceRecordsAsync` (the private helper at line 187+).
- [ ] Confirm each program-filter check is `if (!string.IsNullOrWhiteSpace(programFilter)) studentsQuery = studentsQuery.Where(s => s.Program == programFilter);`.
- [ ] Confirm each grade-filter check is `if (!string.IsNullOrWhiteSpace(gradeFilter)) studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter);`.
- [ ] If any path uses a different shape (e.g., `programFilter ?? "REGULAR"` defaulting), flag and stop — that would be a real bug to fix here.

**Files:** none modified.

### Phase 2: Tests — AttendanceService NG Filter Contract

**Goal:** Lock in AC1–AC4 with integration tests against the in-memory context.

Add a new test class `tests/SmartLog.Web.Tests/Services/AttendanceServiceNonGradedTests.cs` (separate file from any existing `AttendanceServiceTests` to keep US0108 traceability clear).

Tests:
- [ ] **GetAttendanceSummary_ProgramFilter_ExcludesNGStudents**
  - Seed Grade 7 + REGULAR, NG + LEVEL 1. Enroll 1 graded student (Program="REGULAR") + 1 NG student (Program=null) for the day. Add ENTRY scans for both.
  - Call `GetAttendanceSummaryAsync(today, programFilter: "REGULAR")`.
  - Assert `TotalEnrolled == 1` (only graded student counted).

- [ ] **GetAttendanceSummary_NoFilter_IncludesNGStudents**
  - Same seed.
  - Call `GetAttendanceSummaryAsync(today)`.
  - Assert `TotalEnrolled == 2`.

- [ ] **GetAttendanceList_GradeFilterNG_ReturnsOnlyNGStudents**
  - Same seed.
  - Call `GetAttendanceListAsync(today, gradeFilter: "NG")`.
  - Assert returned list has exactly the NG student; graded student absent.

- [ ] **GetAttendanceList_ProgramAndGradeNG_ReturnsEmpty**
  - Same seed.
  - Call `GetAttendanceListAsync(today, gradeFilter: "NG", programFilter: "REGULAR")`.
  - Assert empty list (NG student excluded by program filter; graded student excluded by grade filter).

> **Note:** Use `DbInitializer.SeedNonGradedAsync` to seed NG; reuse `TestDbContextFactory.SeedAll` for graded data. Create students via `TestDbContextFactory.CreateStudent`. Add scans manually via `context.Scans.Add(new Scan { ... })`.

**Files:** `tests/SmartLog.Web.Tests/Services/AttendanceServiceNonGradedTests.cs` (new)

### Phase 3: Build, Test

- [ ] `dotnet build` — clean.
- [ ] `dotnet test --filter "FullyQualifiedName~AttendanceServiceNonGraded"` — green.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` — full suite (excluding pre-existing NoScanAlert failures).

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `tests/SmartLog.Web.Tests/Services/AttendanceServiceNonGradedTests.cs` | Create | 2 |

**No production code modifications expected.**

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Phase 1 turns up a bug (filter logic different than expected) | Fix on the spot if trivial; otherwise pause and report to user |
| Test for ENTRY scan setup is more complex than 4 lines | Keep test seed minimal; share helper if it grows beyond ~8 lines |
| `Scan` entity requires Device foreign key | Either pass null device-id (if FK allows) or seed a dummy Device. Check `Scan` entity definition. |

---

## Open Questions

None — story scope confirmed by user 2026-04-26.

---

## Done Definition

- [ ] All Phase 1-3 tasks checked off.
- [ ] All AC1-AC5 covered by test evidence.
- [ ] Build clean; new tests + full suite (minus NoScanAlert) green.
- [ ] Story status flipped to Review by `code verify`.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Opus 4.7) | Initial plan drafted (narrow scope per US0108 amendment). |
