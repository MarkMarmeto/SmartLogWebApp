# PL0028: Section Create/Edit — Hide Program Dropdown for Non-Graded

> **Status:** Complete
> **Story:** [US0104: Section Create/Edit — Hide Program Dropdown for Non-Graded](../stories/US0104-section-ui-hide-program-for-ng.md)
> **Epic:** EP0010: Programs & Sections Overhaul
> **Created:** 2026-04-26
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages

## Overview

UI-only follow-up to US0103. Today's CreateSection page renders the Program dropdown unconditionally (with a hard-coded `required` attribute) and toggles its options via an existing AJAX handler `?handler=ProgramsForGrade&gradeLevelId=...`. EditSection renders the Program dropdown unconditionally as well. With NG sections having no Program (US0103), the form must hide the Program block entirely when GradeLevel = NG, show a small note, clear any stale value, and toggle the `required` attribute. The server-side validation already enforces the rule (PL0026), so JS-disabled clients still get correct behaviour — they just always see the dropdown.

---

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Hide Program for NG | Selecting NG hides the Program block + shows note "Non-Graded sections do not use a Program" |
| AC2 | Show Program for graded | Selecting any graded level shows the dropdown filtered by the existing AJAX endpoint |
| AC3 | Switching NG → graded clears state | Program dropdown becomes visible with no preselection |
| AC4 | Switching graded → NG clears Program | Program value is nulled on submit |
| AC5 | Server-side validation enforced | Tampered submission still rejected (already covered by PL0026) |
| AC6 | Edit page initial render | Existing NG section renders with Program block hidden + note |

---

## Technical Context

### Language & Framework
- **Primary:** ASP.NET Core 8.0 Razor Pages, vanilla JS (no framework — match existing inline `<script>` pattern in CreateSection.cshtml)

### Key Existing Files
- `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml` (113 lines) — Program block lines 30-40, JS lines 82-112. The Grade options are rendered as `<option value="@grade.Id">@grade.Name</option>` — no Code on the option today. The JS calls `?handler=ProgramsForGrade` and replaces option lists.
- `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml.cs` — `Input.ProgramId` is `Guid?` (post-PL0026). Catch-block routes `InvalidOperationException` to `ModelState[ProgramId]`.
- `src/SmartLog.Web/Pages/Admin/EditSection.cshtml` (82 lines) — Program block lines 24-34. Grade Level is shown as readonly text via `Model.GradeLevelName`. No JS.
- `src/SmartLog.Web/Pages/Admin/EditSection.cshtml.cs` — exposes `GradeLevelName` and `GradeLevelId` properties; **does not expose Code**. Need to add `GradeLevelCode` (or a derived `IsNonGraded` flag).

### NG Identity Contract
A grade level is NG iff `GradeLevel.Code == "NG"` (case-insensitive). This contract was set in PL0026 and PL0027.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Pure UI/binding change. Tests cover the page-handler post paths (Create + Edit, NG vs graded). JS toggle is mechanical and exercised by the manual smoke step.

---

## Implementation Phases

### Phase 1: CreateSection — Render NG-aware Markup

**Goal:** Drop the hardcoded `required` attribute, surface the Code on each Grade option for the JS toggle, add a hidden "no-program" note element, and wrap the Program block with an id for JS to show/hide.

- [ ] In `Pages/Admin/CreateSection.cshtml`:
  - On the Grade `<option>` loop, add `data-code="@grade.Code"` so JS can detect NG without a server round-trip:
    ```razor
    <option value="@grade.Id" data-code="@grade.Code">@grade.Name</option>
    ```
  - Wrap the Program block (lines 30-40) with an id so JS can show/hide it as a unit:
    ```razor
    <div class="mb-3" id="programGroup">
        <label asp-for="Input.ProgramId" class="form-label">Program <span class="text-danger" id="programRequiredMark">*</span></label>
        <select asp-for="Input.ProgramId" class="form-select" id="programSelect">
            <option value="">Select grade level first...</option>
            @foreach (var program in Model.Programs)
            {
                <option value="@program.Id">@program.Code — @program.Name</option>
            }
        </select>
        <span asp-validation-for="Input.ProgramId" class="text-danger"></span>
    </div>
    <div class="mb-3 text-muted small" id="ngProgramNote" hidden>
        Non-Graded sections do not use a Program.
    </div>
    ```
  - **Remove** the hard-coded `required` attribute on the `<select>`. The model is `Guid?` and JS will toggle `required` dynamically.

### Phase 2: CreateSection — JS Toggle Logic

**Goal:** Extend the existing inline `<script>` to:
- Detect NG via `selectedOption.dataset.code === 'NG'`.
- For NG: hide `#programGroup`, show `#ngProgramNote`, clear `#programSelect.value`, remove `required`, **skip** the AJAX fetch.
- For graded: show `#programGroup`, hide `#ngProgramNote`, set `required`, run the existing AJAX fetch.
- For empty grade: revert to the placeholder state shown today.

- [ ] In `CreateSection.cshtml` `@section Scripts`:
  ```html
  <script>
      const gradeSelect = document.getElementById('gradeLevelSelect');
      const programGroup = document.getElementById('programGroup');
      const programSelect = document.getElementById('programSelect');
      const programNote = document.getElementById('ngProgramNote');
      const programMark = document.getElementById('programRequiredMark');

      function setNonGraded(isNg) {
          if (isNg) {
              programSelect.value = '';
              programGroup.hidden = true;
              programNote.hidden = false;
              programSelect.required = false;
              if (programMark) programMark.hidden = true;
          } else {
              programGroup.hidden = false;
              programNote.hidden = true;
              programSelect.required = true;
              if (programMark) programMark.hidden = false;
          }
      }

      gradeSelect.addEventListener('change', function () {
          const opt = this.options[this.selectedIndex];
          const isNg = opt && opt.dataset.code === 'NG';
          setNonGraded(isNg);

          if (isNg) return; // skip AJAX

          const gradeId = this.value;
          programSelect.innerHTML = '<option value="">Loading...</option>';
          programSelect.disabled = true;

          if (!gradeId) {
              programSelect.innerHTML = '<option value="">Select grade level first...</option>';
              programSelect.disabled = false;
              return;
          }

          fetch(`?handler=ProgramsForGrade&gradeLevelId=${gradeId}`)
              .then(r => r.json())
              .then(programs => {
                  programSelect.innerHTML = '<option value="">Select program...</option>';
                  programs.forEach(p => {
                      const opt = document.createElement('option');
                      opt.value = p.id;
                      opt.textContent = `${p.code} — ${p.name}`;
                      programSelect.appendChild(opt);
                  });
                  programSelect.disabled = false;
              })
              .catch(() => {
                  programSelect.innerHTML = '<option value="">Error loading programs</option>';
                  programSelect.disabled = false;
              });
      });

      // On page first load (e.g., post-back with model errors), apply state from current selection.
      const initialOpt = gradeSelect.options[gradeSelect.selectedIndex];
      if (initialOpt && initialOpt.dataset.code === 'NG') {
          setNonGraded(true);
      } else {
          setNonGraded(false);
      }
  </script>
  ```

**Files (Phases 1+2):** `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml`

### Phase 3: EditSection — Page Model Exposes GradeLevel Code

**Goal:** Make the Code available to the cshtml so it can render conditionally on first load.

- [ ] In `Pages/Admin/EditSection.cshtml.cs`:
  - Add property: `public string GradeLevelCode { get; set; } = string.Empty;` next to `GradeLevelName`.
  - In `OnGetAsync`, set `GradeLevelCode = section.GradeLevel.Code;` (alongside the existing `GradeLevelName = section.GradeLevel.Name;` at line 79).
  - In the `InvalidOperationException` catch block (added by PL0026), also set `GradeLevelCode` after re-fetching the section, so the form re-renders correctly post-error.

- [ ] Optional helper property to keep the cshtml tidy: `public bool IsNonGraded => string.Equals(GradeLevelCode, "NG", StringComparison.OrdinalIgnoreCase);`

**Files:** `src/SmartLog.Web/Pages/Admin/EditSection.cshtml.cs`

### Phase 4: EditSection — Conditional Program Block

**Goal:** Render the Program block only for graded levels; show the NG note otherwise. No JS needed (Grade Level is readonly on Edit).

- [ ] In `Pages/Admin/EditSection.cshtml`:
  ```razor
  @if (!Model.IsNonGraded)
  {
      <div class="mb-3">
          <label asp-for="Input.ProgramId" class="form-label">Program <span class="text-danger">*</span></label>
          <select asp-for="Input.ProgramId" class="form-select" required>
              <option value="">Select program...</option>
              @foreach (var program in Model.Programs)
              {
                  <option value="@program.Id">@program.Code — @program.Name</option>
              }
          </select>
          <span asp-validation-for="Input.ProgramId" class="text-danger"></span>
      </div>
  }
  else
  {
      <div class="mb-3 text-muted small">
          Non-Graded sections do not use a Program.
      </div>
  }
  ```
- [ ] Keep the hidden `Input.ProgramId` value safe: when NG, the field is omitted from the form entirely → binder receives nothing → `Guid?` defaults to null on submit. That's the desired contract.

**Files:** `src/SmartLog.Web/Pages/Admin/EditSection.cshtml`

### Phase 5: Tests — Page Handler Integration

**Goal:** Lock in the four state combinations + the EditSection NG-render path.

Add `tests/SmartLog.Web.Tests/Pages/Admin/SectionPagesNonGradedTests.cs`. The test project already has a `Pages/` folder per the earlier `ls`; if not, create it (no namespace clash since `SmartLog.Web.Pages` is for production code, and `SmartLog.Web.Tests.Pages` is fine — the prior collision was `Tests.Data` shadowing `Web.Data`. `Pages` is unambiguous because it's not used unqualified in test files).

Tests:
- [ ] **CreateSectionPage_PostNullProgramForGraded_ReturnsPageWithModelError**
  - Seed Grade 7 + REGULAR. POST `Input.GradeLevelId = grade7.Id, Input.ProgramId = null, Input.Name = "7-A", Input.Capacity = 30`. Assert returns `PageResult` and `ModelState["Input.ProgramId"]` has error "Program is required for graded sections".
- [ ] **CreateSectionPage_PostNullProgramForNG_RedirectsToSections**
  - Seed NG (via `DbInitializer.SeedNonGradedAsync`). POST `Input.GradeLevelId = ng.Id, Input.ProgramId = null, Input.Name = "LEVEL 5", Input.Capacity = 30`. Assert returns `RedirectToPageResult("/Admin/Sections")` and the new section exists with `ProgramId == null`.
- [ ] **EditSectionPage_OnGetForNGSection_ExposesGradeLevelCodeNG**
  - Seed NG + a LEVEL 1 section. Call `OnGetAsync(section.Id)`. Assert `GradeLevelCode == "NG"` and `IsNonGraded == true`.
- [ ] **EditSectionPage_PostProgramOnNGSection_ReturnsModelError**
  - Existing NG section. POST `Input.ProgramId = REGULAR.Id`. Assert `ModelState["Input.ProgramId"]` contains "must not have a Program".

**Files (Phase 5):** `tests/SmartLog.Web.Tests/Pages/Admin/SectionPagesNonGradedTests.cs` (new)

> **Note:** Razor Page handler tests need real or mocked dependencies (`IGradeSectionService`, `ApplicationDbContext`, `IAuditService`, `ILogger<T>`). Use the in-memory context via `TestDbContextFactory.Create()`, real `GradeSectionService`, and a stub `IAuditService` (use Moq's `Mock.Of<IAuditService>()`). If `IAuditService` is missing in test fixtures, mock it inline. Avoid attempting to spin up the full ASP.NET Core test host — direct PageModel construction is sufficient because we only test handler logic.

### Phase 6: Build, Test, Manual Smoke

- [ ] `dotnet build` — clean.
- [ ] `dotnet test --filter "FullyQualifiedName~SectionPagesNonGraded"` — green.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` — full suite (excluding pre-existing NoScanAlert failures).
- [ ] **Manual smoke (recommended):** `dotnet run --project src/SmartLog.Web --urls="http://localhost:5050"` and:
  - Visit `/Admin/CreateSection`. Pick "Non-Graded" → Program block hides, note appears, Submit button OK with no Program selected.
  - Pick "Grade 7" → Program block reappears, AJAX populates programs, submit requires Program.
  - Switch back and forth — values clear correctly.
  - Visit `/Admin/EditSection/{ng-section-id}` (after running app, NG sections were seeded by US0105). Confirm Program block is hidden and note shows.

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml` | Modify (markup + JS) | 1, 2 |
| `src/SmartLog.Web/Pages/Admin/EditSection.cshtml.cs` | Modify (expose Code/IsNonGraded) | 3 |
| `src/SmartLog.Web/Pages/Admin/EditSection.cshtml` | Modify (conditional render) | 4 |
| `tests/SmartLog.Web.Tests/Pages/Admin/SectionPagesNonGradedTests.cs` | Create | 5 |

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| `data-code` attribute clashes with existing CSP rules | None today — vanilla data attributes pass any reasonable CSP. |
| EditSection's `<input type="hidden" asp-for="Input.Id" />` and other binding still posts when Program block is omitted, leading to the binder leaving `Input.ProgramId` unset (default null) | That's the desired behaviour. Tested in Phase 5. |
| Admin had previously set Program on an NG section via direct DB edit before app upgrade | US0105 seed cleanup nulls those on next app boot. After that, the UI behaviour is correct. |
| Test project doesn't have `Pages/Admin/` folder | Creating a folder is fine; namespace `SmartLog.Web.Tests.Pages.Admin` is safe (does not collide with `SmartLog.Web.Pages.Admin` because the prefix differs). |
| `_ValidationScriptsPartial` re-asserts `required` on the dropdown | Unlikely (it's jQuery validate auto-detection). If observed, drop the `required` mark even on graded and rely on server-side enforcement only. Defer to that fallback only if tests / smoke reveal the issue. |

---

## Open Questions

None.

---

## Done Definition

- [ ] All Phase 1-6 tasks checked off.
- [ ] All AC1-AC6 covered by code + test evidence.
- [ ] Build clean; new tests + full suite (minus NoScanAlert) green.
- [ ] Manual smoke confirms toggle in the browser.
- [ ] Story status flipped to Review by `code verify`.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude (Opus 4.7) | Initial plan drafted. |
