# PL0031: Broadcast Targeting — Add Non-Graded Branch Alongside Programs

> **Status:** Complete
> **Story:** [US0107: Broadcast Targeting — Add Non-Graded Branch Alongside Programs](../stories/US0107-broadcast-targeting-ng-branch.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 + EF Core 8.0 + vanilla JS

## Overview

Extend the existing program-first targeting UI (US0084 / `_ProgramFirstTargeting.cshtml` + `wwwroot/js/sms-broadcast-targeting.js`) to expose Non-Graded as a sibling branch with selectable LEVEL 1–4 sections. The backend resolver gains a parallel branch that resolves NG section selections. Wire format stays the same shape (`List<ProgramGradeFilter>`) but the DTO grows an optional `SectionNames` field; an entry with empty `ProgramCode` + populated `SectionNames` denotes an NG selection.

Three SMS composer pages (Announcement, Emergency, BulkSend) all use the partial — they need parallel updates to populate the new wrapper view-model and pass NG sections through.

---

## Acceptance Criteria Summary

| AC | Name | Status |
|----|------|--------|
| AC1 | Non-Graded group renders as sibling to Programs | In scope |
| AC2 | Expanding Non-Graded reveals LEVEL 1–4 checkboxes | In scope |
| AC3 | Granular NG section selection | In scope |
| AC4 | Combined with Program selections (union) | In scope |
| AC5 | "All" shortcut includes NG | In scope |
| AC6 | Filter payload carries NG selection | In scope (`SectionNames` field) |
| AC7 | Preview count reflects NG | In scope |
| AC8 | Empty-selection validation error | **Deviation — see below** |

### AC8 Deviation
Current contract for empty filter list = "send to all SMS-enabled active students". The "All Programs / All Grades" shortcut produces equivalent behaviour. Adding a hard validation error on empty selection conflicts with this established UX from US0084. **Recommendation:** keep current behaviour (empty = all); the UI's pre-checked "All" shortcut means a user has to explicitly un-check to reach empty. The existing edge case of "no SMS-enabled students" is already caught with an error message (see `Announcement.cshtml.cs:158-162`). Document the deviation in the story Revision History; do not implement AC8.

---

## Technical Context

### Language & Framework
- ASP.NET Core 8.0 Razor Pages + EF Core 8.0
- Vanilla JS at `wwwroot/js/sms-broadcast-targeting.js`

### Key Existing Files
- `src/SmartLog.Web/Pages/Admin/Sms/_ProgramFirstTargeting.cshtml` (54 lines) — current `@model List<ProgramWithGrades>`
- `src/SmartLog.Web/wwwroot/js/sms-broadcast-targeting.js` — `init()`, `buildFilters()`, `serializeAndUpdate()`
- `src/SmartLog.Web/Models/Sms/ProgramGradeFilter.cs` — `{ ProgramCode: string, GradeLevelCodes: List<string> }`
- `src/SmartLog.Web/Models/Sms/ProgramWithGrades.cs` — `{ Code, Name, Grades: List<GradeLevelItem> }`
- `src/SmartLog.Web/Services/Sms/SmsService.cs:237` — `ResolveStudentIdsByFiltersAsync`
- `src/SmartLog.Web/Pages/Admin/Sms/{Announcement, Emergency, BulkSend}.cshtml(.cs)` — all use the partial via `<partial name="_ProgramFirstTargeting" model="Model.ProgramsWithGrades" />`

### Student Data Shape Recap
- NG students have `Student.GradeLevel == "NG"` and `Student.Section` in `{"LEVEL 1", "LEVEL 2", "LEVEL 3", "LEVEL 4"}` and `Student.Program == null`.
- Resolution by section name is straightforward via `Students.Where(s => s.GradeLevel == "NG" && sectionNames.Contains(s.Section))`.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Multi-file UI/JS/backend change. Tests cover the resolver contract (the most regression-prone surface). Manual smoke covers UI/JS interactions.

---

## Implementation Phases

### Phase 1: DTO + View-Model Extensions

**Goal:** Extend the wire format and the partial's input model to carry NG section data.

- [ ] Add to `src/SmartLog.Web/Models/Sms/ProgramGradeFilter.cs`:
  ```csharp
  public class ProgramGradeFilter
  {
      public string ProgramCode { get; set; } = string.Empty;
      public List<string> GradeLevelCodes { get; set; } = new();

      /// <summary>
      /// US0107: When ProgramCode is empty and SectionNames is non-empty,
      /// this entry denotes a Non-Graded section selection. Resolver matches
      /// students by GradeLevel == "NG" AND Section IN SectionNames.
      /// </summary>
      public List<string>? SectionNames { get; set; }
  }
  ```
- [ ] Create `src/SmartLog.Web/Models/Sms/BroadcastTargetingViewModel.cs`:
  ```csharp
  namespace SmartLog.Web.Models.Sms;

  public class BroadcastTargetingViewModel
  {
      public List<ProgramWithGrades> ProgramsWithGrades { get; set; } = new();
      public List<NonGradedSectionItem> NonGradedSections { get; set; } = new();
  }

  public class NonGradedSectionItem
  {
      public string Name { get; set; } = string.Empty; // e.g., "LEVEL 1"
  }
  ```

**Files:** `Models/Sms/ProgramGradeFilter.cs`, `Models/Sms/BroadcastTargetingViewModel.cs` (new)

### Phase 2: Update the Partial Markup

**Goal:** Render NG group below Programs.

- [ ] In `src/SmartLog.Web/Pages/Admin/Sms/_ProgramFirstTargeting.cshtml`:
  - Change `@model List<ProgramWithGrades>` → `@model BroadcastTargetingViewModel`
  - Replace `@foreach (var prog in Model)` with `@foreach (var prog in Model.ProgramsWithGrades)`
  - After the program `@foreach` block, before the `<input type="hidden">`, render the NG group:
    ```razor
    @if (Model.NonGradedSections.Any())
    {
        var ngHasSections = Model.NonGradedSections.Any();
        <div class="border rounded p-2 mb-2 program-entry ng-entry">
            <div class="form-check mb-1">
                <input class="form-check-input nongraded-cb" type="checkbox"
                       id="nongraded-group"
                       @(ngHasSections ? "" : "disabled")
                       checked="@ngHasSections" />
                <label class="form-check-label fw-semibold" for="nongraded-group">
                    Non-Graded
                </label>
            </div>
            <div class="grade-list ms-4">
                @foreach (var sec in Model.NonGradedSections)
                {
                    <div class="form-check form-check-inline">
                        <input class="form-check-input nongraded-section-cb" type="checkbox"
                               id="ngsec_@sec.Name.Replace(' ', '_')"
                               data-section-name="@sec.Name"
                               checked />
                        <label class="form-check-label small" for="ngsec_@sec.Name.Replace(' ', '_')">
                            @sec.Name
                        </label>
                    </div>
                }
            </div>
        </div>
    }
    ```

**Files:** `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml`

### Phase 3: Update JS — NG Branch in `buildFilters()` + State Wiring

**Goal:** Capture NG selections and emit them in the JSON, propagate "All" shortcut, drive indeterminate state.

- [ ] In `src/SmartLog.Web/wwwroot/js/sms-broadcast-targeting.js`:
  - In `init()`, add listeners for `.nongraded-cb` (parent toggle) and `.nongraded-section-cb` (child sections), mirroring the program/grade pattern (parent checks/unchecks all children; child changes update parent indeterminate).
  - In the "select all" handler, also flip `.nongraded-cb` and `.nongraded-section-cb` checked state.
  - In `updateSelectAllState()`, include NG group in the count (treat NG as a peer of programs).
  - In `buildFilters()`, after the program loop, add:
    ```js
    var ngParent = document.querySelector('.nongraded-cb:not([disabled])');
    if (ngParent) {
        var sectionNames = [];
        document.querySelectorAll('.nongraded-section-cb:checked').forEach(function (s) {
            sectionNames.push(s.dataset.sectionName);
        });
        if (sectionNames.length > 0) {
            filters.push({ programCode: '', gradeLevelCodes: [], sectionNames: sectionNames });
        }
    }
    ```
  - Sanity-check serialisation: empty NG branch (parent unchecked) emits no entry; partial selection emits an entry with the chosen sections only.

**Files:** `wwwroot/js/sms-broadcast-targeting.js`

### Phase 4: Resolver Branch in `SmsService`

**Goal:** Recognise NG-section filter entries and resolve them.

- [ ] In `src/SmartLog.Web/Services/Sms/SmsService.cs:237` `ResolveStudentIdsByFiltersAsync`, replace the per-filter loop body with:
  ```csharp
  foreach (var filter in filters)
  {
      IQueryable<Student> query;

      // US0107: Non-Graded branch — empty ProgramCode + populated SectionNames.
      if (string.IsNullOrEmpty(filter.ProgramCode) && filter.SectionNames is { Count: > 0 })
      {
          query = _context.Students
              .Where(s => s.GradeLevel == "NG" && filter.SectionNames.Contains(s.Section));
      }
      else if (!string.IsNullOrEmpty(filter.ProgramCode))
      {
          query = _context.Students.Where(s => s.Program == filter.ProgramCode);
          if (filter.GradeLevelCodes.Count > 0)
              query = query.Where(s => filter.GradeLevelCodes.Contains(s.GradeLevel));
      }
      else
      {
          continue; // ill-formed filter
      }

      if (activeOnly) query = query.Where(s => s.IsActive);
      if (smsEnabledOnly) query = query.Where(s => s.SmsEnabled);

      var ids = await query.Select(s => s.Id).ToListAsync();
      foreach (var id in ids) allIds.Add(id);
  }
  ```
- [ ] Empty filter list still falls through to "all SMS-enabled active students" (existing behaviour at lines 242-248) — unchanged.

**Files:** `Services/Sms/SmsService.cs`

### Phase 5: Composer Page Updates

**Goal:** Populate the new view-model in all three composers.

For each of `Announcement.cshtml.cs`, `Emergency.cshtml.cs`, `BulkSend.cshtml.cs`:
- [ ] Replace `public List<ProgramWithGrades> ProgramsWithGrades { get; set; }` with `public BroadcastTargetingViewModel Targeting { get; set; } = new();`
- [ ] In `LoadPageDataAsync` (or equivalent): keep building `ProgramsWithGrades` exactly as today, but assign to `Targeting.ProgramsWithGrades`. Then populate `Targeting.NonGradedSections`:
  ```csharp
  Targeting.NonGradedSections = await _context.Sections
      .Include(s => s.GradeLevel)
      .Where(s => s.IsActive && s.GradeLevel.Code == "NG")
      .OrderBy(s => s.Name)
      .Select(s => new NonGradedSectionItem { Name = s.Name })
      .ToListAsync();
  ```
- [ ] In each `.cshtml`: change `<partial name="_ProgramFirstTargeting" model="Model.ProgramsWithGrades" />` → `<partial name="_ProgramFirstTargeting" model="Model.Targeting" />`

**Files:** `Pages/Admin/Sms/Announcement.cshtml(.cs)`, `Emergency.cshtml(.cs)`, `BulkSend.cshtml(.cs)` (6 files)

### Phase 6: History Logging Audit

**Goal:** When NG sections are targeted, the broadcast history should record something sensible (currently the composers log `historyPrograms` and `historyGrades` derived from filters — NG entries have empty ProgramCode, so they'd show as blank).

- [ ] Inspect `Announcement.cshtml.cs:173-179` (and the same block in Emergency/BulkSend). The `historyPrograms` / `historyGrades` derived from filters today only includes program-entry data. Adjust to:
  ```csharp
  var historyPrograms = filters
      .Where(f => !string.IsNullOrEmpty(f.ProgramCode))
      .Select(f => f.ProgramCode)
      .Distinct()
      .ToList();

  var hasNg = filters.Any(f => string.IsNullOrEmpty(f.ProgramCode) && f.SectionNames is { Count: > 0 });
  if (hasNg) historyPrograms.Add("Non-Graded");
  ```
- [ ] Repeat in Emergency and BulkSend handlers.

**Files:** same 3 page handlers as Phase 5.

### Phase 7: Tests — Resolver Contract

**Goal:** Lock in NG branch resolution + program-grade preservation.

Add `tests/SmartLog.Web.Tests/Services/Sms/SmsServiceNonGradedTargetingTests.cs` (or extend existing `ProgramFirstTargetingTests.cs` if present).

Tests:
- [ ] **ResolveStudentIdsByFilters_OnlyProgramFilter_ExcludesNGStudents** — Seed 1 graded (Program=REGULAR) + 1 NG student. Filter: `[{ ProgramCode: "REGULAR", GradeLevelCodes: ["7"] }]`. Assert only graded student returned.
- [ ] **ResolveStudentIdsByFilters_OnlyNGFilter_ReturnsOnlyNGStudents** — Same seed. Filter: `[{ ProgramCode: "", SectionNames: ["LEVEL 1"] }]`. Assert only NG student returned.
- [ ] **ResolveStudentIdsByFilters_CombinedProgramAndNG_UnionsResults** — Same seed. Filter: `[{ Program: REGULAR, Grades: [7] }, { Program: "", SectionNames: [LEVEL 1] }]`. Assert both students returned (set, no duplicates).
- [ ] **ResolveStudentIdsByFilters_NGFilterWithSpecificSection_FiltersToThatSection** — Seed 2 NG students in different LEVEL sections. Filter: `[{ Program: "", SectionNames: ["LEVEL 2"] }]`. Assert only the LEVEL 2 student returned.
- [ ] **ResolveStudentIdsByFilters_NGFilterRespectsActiveAndSmsEnabledFlags** — Seed 1 NG with `SmsEnabled=false`. Assert excluded by default.

**Files:** `tests/SmartLog.Web.Tests/Services/Sms/SmsServiceNonGradedTargetingTests.cs` (new) — or extend existing `ProgramFirstTargetingTests.cs` if its structure fits.

### Phase 8: Build, Test, Manual Smoke

- [ ] `dotnet build` — clean.
- [ ] `dotnet test --filter "FullyQualifiedName~SmsService|FullyQualifiedName~ProgramFirstTargeting"` — green.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` — full suite green.
- [ ] **Manual smoke (recommended):** run the app, open `/Admin/Sms/Announcement`. Verify Non-Graded group renders below programs with LEVEL 1-4. Tick combinations (NG only, Programs+NG, partial NG) and confirm the recipient count endpoint reflects the selection.

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `Models/Sms/ProgramGradeFilter.cs` | Modify (add SectionNames) | 1 |
| `Models/Sms/BroadcastTargetingViewModel.cs` | Create | 1 |
| `Pages/Admin/Sms/_ProgramFirstTargeting.cshtml` | Modify (model + NG group) | 2 |
| `wwwroot/js/sms-broadcast-targeting.js` | Modify (NG branch) | 3 |
| `Services/Sms/SmsService.cs` | Modify (resolver branch) | 4 |
| `Pages/Admin/Sms/Announcement.cshtml(.cs)` | Modify (view-model + history) | 5, 6 |
| `Pages/Admin/Sms/Emergency.cshtml(.cs)` | Modify (view-model + history) | 5, 6 |
| `Pages/Admin/Sms/BulkSend.cshtml(.cs)` | Modify (view-model + history) | 5, 6 |
| `tests/SmartLog.Web.Tests/Services/Sms/SmsServiceNonGradedTargetingTests.cs` | Create | 7 |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Existing `ProgramFirstTargetingTests` (if present) breaks because the resolver loop changed | Phase 4 keeps the program-grade branch identical in semantics — only restructured. Run that test class explicitly in Phase 8. |
| JS state machine ("All" shortcut, indeterminate) becomes brittle with NG added | Mirror the existing program-grade pattern exactly. Manual smoke catches edge cases the unit tests can't. |
| `.cshtml` partial path uses `name="_ProgramFirstTargeting"` — ASP.NET partial discovery is by file name, not type | Filename unchanged; only `@model` line changes. Partial discovery still works. |
| EF cannot translate `filter.SectionNames.Contains(s.Section)` when `SectionNames` is a closure variable | EF Core 8 translates `List<T>.Contains(column)` fine. Prove with the resolver tests in Phase 7. |
| BroadcastTargetingViewModel name collides with future expansion | Acceptable — it's narrowly scoped to broadcast composers. |

---

## Open Questions

None.

---

## Done Definition

- [ ] All Phase 1-8 tasks checked off.
- [ ] All AC1-AC7 covered by code + test evidence (AC8 explicitly deviated — see Overview).
- [ ] Build clean; new tests + full suite (minus NoScanAlert) green.
- [ ] Manual smoke passes for at least Announcement composer.
- [ ] Story status flipped to Review by `code verify`.
- [ ] Story Revision History notes the AC8 deviation.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Opus 4.7) | Initial plan drafted. AC8 deviation documented (empty filter list = "all" preserved per US0084 UX). |
