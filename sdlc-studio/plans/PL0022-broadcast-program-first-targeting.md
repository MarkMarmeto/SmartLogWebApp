# PL0022: Broadcast Targeting — Program-First with Nested Grade Levels

> **Status:** Complete
> **Story:** [US0084: Broadcast Targeting — Program-First with Nested Grade Levels](../stories/US0084-broadcast-program-first-targeting.md)
> **Epic:** EP0009: SMS Strategy Overhaul
> **Created:** 2026-04-25
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages + JavaScript

## Overview

Replace the existing Grade Level → Program secondary-filter UI on all three broadcast composer pages (Announcement, Emergency, BulkSend) with a Program-first nested UI. A new shared Razor partial renders Programs as expandable top-level checkboxes; each Program expands to show its allowed Grade Levels (from `GradeLevelProgram` junction). A companion JS module maintains checkbox state, computes preview counts, and posts the filter as `[{programId, gradeLevelIds[]}, ...]`.

The backend target resolver is extended to accept this new filter shape alongside the existing shape for backward compatibility.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Program-first UI | Programs as top-level checkboxes; nested grade levels per `GradeLevelProgram` |
| AC2 | Select → auto-expand | Ticking a Program expands + auto-selects all its grades |
| AC3 | Granular grade selection | Can un-tick individual grades within a Program |
| AC4 | Multi-program targeting | Union of per-Program grade selections |
| AC5 | "All Programs" shortcut | Top-of-list checkbox selects everything |
| AC6 | Preview count | "Sending to N students" updates on selection change |
| AC7 | Filter payload | Posts `[{programId, gradeLevelIds[]}, ...]`; resolver converts to student list |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
- **Architecture:** Razor Pages + EF Core 8.0 + JavaScript (vanilla or existing jQuery)
- **Test Framework:** xUnit + Moq

### Key Existing Patterns
- **Broadcast pages:** `Pages/Admin/Sms/Announcement.cshtml(.cs)`, `Emergency.cshtml(.cs)`, `BulkSend.cshtml(.cs)` — all use `[BindProperty]` form models
- **Target resolver:** `Services/Sms/BroadcastTargetResolver.cs` (or equivalent) — currently accepts grade-level + secondary program filters
- **GradeLevelProgram junction:** `GradeLevelProgram` entity; `GradeLevelProgramRepository` (or inline DbContext query) — seeded in EP0010
- **Preview count endpoint:** existing AJAX endpoint (check `Pages/Admin/Sms/` for a `GetRecipientCount` handler or similar)

---

## Recommended Approach

**Strategy:** Test-After  
**Rationale:** UI-heavy component + JS state management. Tests cover the target resolver's new filter shape and server-side partial rendering. JS state is tested via manual smoke-test on all three composer pages.

---

## Implementation Phases

### Phase 1: Data — Programs-with-Grades Query

**Goal:** Expose a query method that returns all active Programs with their allowed Grade Levels.

- [ ] In `GradeLevelProgramRepository.cs` (or `ProgramRepository`), add:
  ```csharp
  Task<List<ProgramWithGrades>> GetActiveProgramsWithGradesAsync();
  // ProgramWithGrades { int ProgramId, string Code, string Name, List<GradeLevel> Grades }
  ```
- [ ] Implement: `Programs.Where(p => p.IsActive).Include(p => p.GradeLevelPrograms).ThenInclude(glp => glp.GradeLevel).OrderBy(p => p.Code)`
- [ ] Return only active Programs; exclude Programs with no junction rows (handled in UI with tooltip, not excluded from query).

**Files:** `Services/ProgramRepository.cs` (or `GradeLevelProgramRepository.cs`)

### Phase 2: Shared Targeting Partial

**Goal:** New Razor partial `_ProgramFirstTargeting.cshtml` that renders the Program-first checkbox tree.

- [ ] Create `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml`:
  - Top-of-list: `<input type="checkbox" id="select-all-programs">` labelled "All Programs / All Grades"
  - For each `ProgramWithGrades`:
    - Top-level `<input type="checkbox" class="program-cb" data-program-id="{id}">` with program Code + Name
    - Nested `<ul class="grade-list">` containing `<input type="checkbox" class="grade-cb" data-program-id="{id}" data-grade-id="{gradeId}">` per grade
    - Programs with zero grades: show row with `disabled` + tooltip "No grades configured"
  - Hidden field `<input type="hidden" name="TargetingJson" id="targeting-json">` — populated by JS before form submit
- [ ] Create `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml.cs` (PageModel partial, if needed) or inject the `List<ProgramWithGrades>` from the parent page model.

**Files:** `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml`, `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml.cs` (optional)

### Phase 3: JS Module

**Goal:** Maintain checkbox state, sync indeterminate state, update preview, serialize payload.

- [ ] Create `wwwroot/js/sms-broadcast-targeting.js`:
  - On program-checkbox click: expand nested list; auto-select all grades if ticking; set indeterminate if some grades un-ticked.
  - On grade-checkbox click: update parent program's checked/indeterminate state.
  - "All Programs" checkbox: select/deselect everything.
  - On any change: serialize state to `[{programId, gradeLevelIds[]}, ...]`, write to `#targeting-json`, trigger preview-count fetch.
  - On form submit: ensure `#targeting-json` is populated before submit.
- [ ] Preview fetch: POST/GET to existing `?handler=RecipientCount` with the new filter JSON; display count in the existing "Sending to N students" label.

**Files:** `wwwroot/js/sms-broadcast-targeting.js`

### Phase 4: Backend — New Filter Shape

**Goal:** Extend the target resolver to accept `List<ProgramGradeFilter>`.

- [ ] Create `Models/Sms/ProgramGradeFilter.cs`:
  ```csharp
  public class ProgramGradeFilter {
      public int ProgramId { get; set; }
      public List<int> GradeLevelIds { get; set; } = new();
  }
  ```
- [ ] In `BroadcastTargetResolver` (or wherever student IDs are resolved), add method:
  ```csharp
  Task<List<int>> ResolveStudentIdsAsync(List<ProgramGradeFilter> filters);
  // For each filter: Students WHERE ProgramId = X AND GradeLevelId IN (Y1, Y2, ...)
  // UNION all results; distinct student IDs
  ```
- [ ] Existing resolver overload remains untouched for callers not yet migrated.

**Files:** `Models/Sms/ProgramGradeFilter.cs`, `Services/Sms/BroadcastTargetResolver.cs`

### Phase 5: Wire into Broadcast Pages

**Goal:** Replace existing targeting section on all three composer pages.

- [ ] In each of `Announcement.cshtml(.cs)`, `Emergency.cshtml(.cs)`, `BulkSend.cshtml(.cs)`:
  - Inject `ProgramWithGrades` list into page model (`OnGetAsync`).
  - Replace existing Grade Level / Program filter section with `<partial name="_ProgramFirstTargeting" model="Model.ProgramsWithGrades" />`.
  - Parse `TargetingJson` in `OnPostAsync`, deserialize to `List<ProgramGradeFilter>`, pass to resolver.
  - Validation: if `TargetingJson` is empty / resolves to zero students → `ModelState.AddModelError`.
- [ ] Add `RecipientCount` handler to each page (or a shared AJAX endpoint):
  ```csharp
  public async Task<JsonResult> OnGetRecipientCountAsync([FromQuery] string targetingJson)
  {
      var filters = JsonSerializer.Deserialize<List<ProgramGradeFilter>>(targetingJson);
      var count = await _targetResolver.ResolveStudentIdsAsync(filters);
      return new JsonResult(new { count = count.Count });
  }
  ```

**Files:** `Pages/Admin/Sms/Announcement.cshtml(.cs)`, `Pages/Admin/Sms/Emergency.cshtml(.cs)`, `Pages/Admin/Sms/BulkSend.cshtml(.cs)`

### Phase 6: Tests

| AC | Test | File |
|----|------|------|
| AC3 | Resolver returns correct student union for multi-program filter | `BroadcastTargetResolverTests.cs` |
| AC7 | Filter payload deserialized and resolved correctly | same |
| AC1 | Partial renders correct program rows and nested grades | `ProgramFirstTargetingTests.cs` (view component / model test) |

- [ ] Run `dotnet test`; confirm zero regressions.

---

## Edge Case Handling

| # | Edge Case | Handling |
|---|-----------|----------|
| 1 | Program has no grades | Row shown with `disabled` attribute + tooltip; cannot be selected; resolver ignores it |
| 2 | All grades un-ticked under a Program | Program CB indeterminate; target count re-computes (effectively 0 for that program) |
| 3 | Inactive Programs | Not returned by query; not shown |
| 4 | No Programs selected on submit | `ModelState.AddModelError` — "Select at least one Program + Grade Level" |
| 5 | `GradeLevelProgram` junction extended with new programs | Query picks them up automatically; no code change needed |

---

## Definition of Done

- [ ] `_ProgramFirstTargeting.cshtml` renders correctly on all three composer pages
- [ ] JS state management: select-all, per-program, per-grade, indeterminate states work
- [ ] Preview count updates on selection change
- [ ] `TargetingJson` posted and deserialized in `OnPostAsync`
- [ ] Target resolver accepts `List<ProgramGradeFilter>` and returns correct student union
- [ ] Existing resolver overload untouched
- [ ] Tests passing; `dotnet test` clean

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-25 | Claude | Initial plan |
