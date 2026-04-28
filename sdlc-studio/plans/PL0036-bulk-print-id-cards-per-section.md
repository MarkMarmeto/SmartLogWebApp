# PL0036: Bulk Print ID Cards per Section

> **Status:** Complete
> **Story:** [US0113: Bulk Print ID Cards per Section](../stories/US0113-bulk-print-id-cards-per-section.md)
> **Epic:** EP0013: QR Permanence & Card Redesign
> **Created:** 2026-04-27
> **Language:** C# 12 / ASP.NET Core 8.0 Razor Pages
> **Drafted by:** Claude (Opus 4.7)

## Overview

Add a section-scoped bulk print page at `/Admin/PrintIdCards/Section/{id}` that renders one CR80 card per active enrollment in that section, arranged on A4 sheets (2 columns × 5 rows = 10 cards/page). Reuses `_StudentIdCard.cshtml` from PL0035 — zero card-design duplication. Adds a "Print ID Cards" entry point on the Sections list page.

Browser print only (no PDF library) — same fidelity, zero new dependencies.

---

## Acceptance Criteria Summary

| AC | Name | Implementation Phase |
|----|------|----------------------|
| AC1 | Entry point on Sections list/detail | Phase 4 |
| AC2 | Active enrollments only, current AY; QR-less students excluded with warning | Phase 2 |
| AC3 | A4 sheet 2×5 grid at exact CR80 dimensions | Phase 3 (CSS) |
| AC4 | Visual parity with single-print card | Phase 3 (partial reuse) |
| AC5 | Print hides screen controls + warning banner | Phase 3 |
| AC6 | Section header on screen only | Phase 3 |
| AC7 | `CanManageStudents` policy | Phase 2 |
| AC8 | Non-existent section returns 404 | Phase 2 |
| AC9 | Empty section shows friendly message | Phase 2 |
| AC10 | Performance ≤ 3s for 50 students; no N+1 | Phase 2 (single query with includes) |

---

## Technical Context

### Depends On
- **PL0035 / US0112** must land first — this plan reuses `_StudentIdCard.cshtml` and `StudentIdCardViewModel`.

### Page Route Shape
`@page "/Admin/PrintIdCards/Section/{sectionId:guid}"` — leaves room for future `/Admin/PrintIdCards/Grade/{gradeId}` or `/Admin/PrintIdCards/Custom?ids=...` without ambiguous query parameters.

### Query
```csharp
var students = await _db.StudentEnrollments
    .Where(e => e.SectionId == sectionId
             && e.IsActive
             && e.AcademicYear.IsCurrent)
    .Include(e => e.Student).ThenInclude(s => s.QrCodes)
    .Select(e => e.Student)
    .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
    .ToListAsync();
```

Then split in-memory:
```csharp
ValidStudents = students.Where(s => s.QrCodes.Any(q => q.IsValid)).ToList();
SkippedStudents = students.Where(s => !s.QrCodes.Any(q => q.IsValid)).ToList();
```

### Branding Once, Per Page
Read the three branding settings (`System.SchoolName`, `Branding:SchoolLogoPath`, `Branding:ReturnAddressText`) **once** in the handler, then build the per-card view models from the shared values. Avoids repeated AppSettings reads inside the loop.

### Why 2×5 Grid (Not 3×3 or 4×2)
- 2 columns × 5 rows on A4 portrait = 10 cards per page, comfortable margins.
- Card width 85.6mm × 2 = 171.2mm + ~10mm gap → fits A4 width 210mm with 18mm side margins.
- Card height 54mm × 5 = 270mm + ~12mm gaps → fits A4 height 297mm with ~7mm top/bottom margins.
- 3×3 (9 cards) is also viable but wastes vertical space; stick with 2×5.

### A4 Print CSS

```css
@page { size: A4 portrait; margin: 8mm; }
.card-grid {
    display: grid;
    grid-template-columns: 85.6mm 85.6mm;
    gap: 4mm;
    justify-content: center;
}
.card-grid .id-card { page-break-inside: avoid; break-inside: avoid; }
```

`page-break-inside: avoid` (legacy) plus `break-inside: avoid` (modern) ensures no card splits across pages — AC10 in story.

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** Logic surface is small (one query + filter). The visual layout is reused from PL0035. Smoke covers print fidelity; unit tests cover the query and authorization.

---

## Implementation Phases

### Phase 1: Page Skeleton + Auth

**Goal:** Route works, returns 404 / 403 correctly.

- [ ] Create `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml.cs`:
  ```csharp
  [Authorize(Policy = "CanManageStudents")]
  public class PrintIdCardsModel : PageModel
  {
      private readonly ApplicationDbContext _db;
      private readonly IAppSettingsService _appSettings;

      public PrintIdCardsModel(ApplicationDbContext db, IAppSettingsService appSettings)
      {
          _db = db;
          _appSettings = appSettings;
      }

      public Section Section { get; set; } = null!;
      public List<StudentIdCardViewModel> Cards { get; set; } = new();
      public List<Student> SkippedStudents { get; set; } = new();

      public async Task<IActionResult> OnGetSectionAsync(Guid sectionId) { /* Phase 2 */ }
  }
  ```
- [ ] Create `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml`:
  - `@page "/Admin/PrintIdCards/Section/{sectionId:guid}"`
  - `Layout = null` (matches `PrintQrCode.cshtml`)
  - Stub markup: just `<h1>Print ID Cards — @Model.Section.Name</h1>` for now

**Files:**
- `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml` (new)
- `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml.cs` (new)

### Phase 2: Handler Logic — Query + Branding Load

**Goal:** Load section, students, branding; partition valid vs skipped.

- [ ] Implement `OnGetSectionAsync(Guid sectionId)`:
  ```csharp
  var section = await _db.Sections
      .Include(s => s.GradeLevel)
      .FirstOrDefaultAsync(s => s.Id == sectionId);
  if (section == null) return NotFound();
  Section = section;

  var students = await _db.StudentEnrollments
      .Where(e => e.SectionId == sectionId
               && e.IsActive
               && e.AcademicYear.IsCurrent)
      .Include(e => e.Student).ThenInclude(s => s.QrCodes)
      .Select(e => e.Student)
      .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
      .ToListAsync();

  var schoolName = await _appSettings.GetAsync("System.SchoolName") ?? "SmartLog School";
  var logoPath = await _appSettings.GetAsync("Branding:SchoolLogoPath");
  var returnAddress = await _appSettings.GetAsync("Branding:ReturnAddressText");

  foreach (var student in students)
  {
      var validQr = student.QrCodes.FirstOrDefault(q => q.IsValid);
      if (validQr == null)
      {
          SkippedStudents.Add(student);
          continue;
      }
      Cards.Add(new StudentIdCardViewModel
      {
          Student = student,
          QrCode = validQr,
          SchoolName = schoolName,
          SchoolLogoPath = logoPath,
          ReturnAddressText = returnAddress,
      });
  }
  return Page();
  ```
- [ ] Confirm the EF query produces a single SQL statement — log SQL during smoke: `dotnet run` then check console for one SELECT with the JOIN, no per-student queries.

**Files:** `PrintIdCards.cshtml.cs` (modify)

### Phase 3: Markup + Print CSS

**Goal:** Render the grid; reuse the partial; print correctly.

- [ ] Update `PrintIdCards.cshtml`:
  ```razor
  @page "/Admin/PrintIdCards/Section/{sectionId:guid}"
  @model SmartLog.Web.Pages.Admin.PrintIdCardsModel
  @{ Layout = null; }

  <!DOCTYPE html>
  <html>
  <head>
      <meta charset="utf-8" />
      <title>Print ID Cards — @Model.Section.Name</title>
      <style>
          /* page chrome — screen only */
          body { font-family: 'Segoe UI', Arial, sans-serif; background: #f0f0f0; padding: 24px; }
          .controls { max-width: 800px; margin: 0 auto 16px; display: flex; gap: 10px; align-items: center; }
          .controls h1 { font-size: 16px; flex: 1; }
          .btn { padding: 8px 18px; border-radius: 6px; font-size: 13px; font-weight: 600; cursor: pointer; border: none; }
          .btn-primary { background: #2C5F5D; color: #fff; }
          .btn-secondary { background: #fff; color: #555; border: 1px solid #ccc; }

          .skip-warning { max-width: 800px; margin: 0 auto 16px; padding: 10px 14px; background: #fff3cd; border: 1px solid #ffeeba; border-radius: 4px; color: #856404; font-size: 13px; }

          .empty { max-width: 800px; margin: 80px auto; text-align: center; color: #888; font-size: 16px; }

          /* grid — 2 columns × 5 rows per A4 page */
          .card-grid {
              display: grid;
              grid-template-columns: 85.6mm 85.6mm;
              gap: 4mm;
              justify-content: center;
              max-width: 800px;
              margin: 0 auto;
          }

          @@media print {
              @@page { size: A4 portrait; margin: 8mm; }
              body { background: #fff; padding: 0; margin: 0; }
              .controls, .skip-warning, .empty { display: none !important; }
              .card-grid { gap: 4mm; max-width: none; }
              .id-card { page-break-inside: avoid; break-inside: avoid; }
          }
      </style>
  </head>
  <body>
      <div class="controls">
          <h1>@Model.Section.GradeLevel.Name — @Model.Section.Name &middot; @Model.Cards.Count student@(Model.Cards.Count == 1 ? "" : "s")</h1>
          @if (Model.Cards.Count > 0)
          {
              <button class="btn btn-primary" onclick="window.print()">&#x1F5A8; Print</button>
          }
          <button class="btn btn-secondary" onclick="window.close()">&#x2715; Close</button>
      </div>

      @if (Model.SkippedStudents.Count > 0)
      {
          <div class="skip-warning">
              <strong>@Model.SkippedStudents.Count student@(Model.SkippedStudents.Count == 1 ? "" : "s") skipped</strong> — no valid QR code:
              <ul style="margin: 6px 0 0 18px; padding: 0;">
                  @foreach (var s in Model.SkippedStudents)
                  {
                      <li>
                          @s.FullName (@s.StudentId) —
                          <a href="/Admin/StudentDetails/@s.Id">view profile</a>
                      </li>
                  }
              </ul>
          </div>
      }

      @if (Model.Cards.Count == 0)
      {
          <div class="empty">No students to print for this section.</div>
      }
      else
      {
          <div class="card-grid">
              @foreach (var card in Model.Cards)
              {
                  <partial name="_StudentIdCard" model="card" />
              }
          </div>
      }
  </body>
  </html>
  ```

**Files:** `PrintIdCards.cshtml` (rewrite)

### Phase 4: Entry Point on Sections List

**Goal:** Admin can navigate to bulk print from where they manage sections.

- [ ] Open `src/SmartLog.Web/Pages/Admin/Sections.cshtml`. In the actions column for each Section row, add:
  ```razor
  <a href="/Admin/PrintIdCards/Section/@section.Id" class="btn btn-sm btn-outline-primary" title="Print ID cards for this section">
      &#x1F5A8; Print IDs
  </a>
  ```
  Match the existing button styling pattern in that file.
- [ ] (Optional) If there is a `SectionDetails.cshtml` page, add the same button to its action area.

**Files:** `src/SmartLog.Web/Pages/Admin/Sections.cshtml` (modify)

### Phase 5: Tests

- [ ] `tests/SmartLog.Web.Tests/Pages/PrintIdCardsPageTests.cs`:
  - **`OnGetSectionAsync_NonExistentSection_ReturnsNotFound`**
  - **`OnGetSectionAsync_EmptySection_ReturnsPageWithZeroCards`**
  - **`OnGetSectionAsync_SectionWithStudents_ReturnsAllActiveCurrentAyEnrollments`** — seed 3 active + 1 inactive in current AY + 1 active in prior AY → assert `Cards.Count == 3`.
  - **`OnGetSectionAsync_StudentWithoutValidQr_GoesToSkippedList`** — seed student with `QrCode.IsValid = false` → in `SkippedStudents`, not in `Cards`.
  - **`OnGetSectionAsync_BrandingLoadedOnce_AppliedToAllCards`** — set school name "Test School"; assert each `StudentIdCardViewModel.SchoolName == "Test School"`.
  - **`OnGetSectionAsync_OrderedByLastNameThenFirstName`** — seed Santos→Maria, Cruz→Ana, Cruz→Ben; assert Cards ordered Cruz/Ana, Cruz/Ben, Santos/Maria.
- [ ] EF logging check: in one of the tests, capture SQL via `Microsoft.Extensions.Logging` and assert there's exactly one `SELECT` against `StudentEnrollments` with the expected JOINs (no per-student SELECT). Skip if test infra doesn't support easily — rely on smoke for N+1.

**Files:** `tests/SmartLog.Web.Tests/Pages/PrintIdCardsPageTests.cs` (new)

### Phase 6: Manual Smoke

- [ ] Seed (or use existing) a section with 30+ active students in the current AY.
- [ ] Visit `/Admin/Sections`. Confirm "Print IDs" button visible per row.
- [ ] Click it. Page loads ≤ 3s, shows "Grade X — SectionName · 30 students" header, grid renders 30 cards.
- [ ] DevTools network tab — only one HTML request. EF SQL log (in `dotnet run` console) — one `SELECT ... FROM StudentEnrollments` with JOINs, no per-student follow-up SELECTs.
- [ ] Click Print → preview shows 3 A4 pages: page 1 has 10 cards, page 2 has 10, page 3 has 10. Each card is exactly 85.6mm × 54mm.
- [ ] Confirm screen controls + warning banner do NOT appear in the print preview.
- [ ] Add a deactivated student; reload — count drops by 1, no error.
- [ ] Mark one student's QR `IsValid = false`; reload — that student appears in the warning list, not in the grid.
- [ ] Open the URL with a non-existent GUID → 404.
- [ ] Sign in as Teacher; visit URL → 403.
- [ ] Print actual A4 paper (or print-to-PDF) — measure card dimensions with a ruler / PDF rule. CR80 confirmed.

### Phase 7: Build, Test, Update Story

- [ ] `dotnet build` clean.
- [ ] `dotnet test --filter "FullyQualifiedName!~NoScanAlert"` passes.
- [ ] Update US0113 status → Review.
- [ ] Update US0022 with a "Superseded by US0113 on 2026-MM-DD" note in Revision History (do not change US0022's "Done" status — historical record).

---

## Files to Create / Modify

| File | Action | Phase |
|------|--------|-------|
| `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml` | Create | 1, 3 |
| `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml.cs` | Create | 1, 2 |
| `src/SmartLog.Web/Pages/Admin/Sections.cshtml` | Modify (entry point) | 4 |
| `tests/SmartLog.Web.Tests/Pages/PrintIdCardsPageTests.cs` | Create | 5 |

No DB migration. No new services. No DI registrations.

---

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| `_StudentIdCard.cshtml` partial path resolution fails in nested folder | Use full path: `<partial name="/Pages/Admin/_StudentIdCard.cshtml" model="card" />` if the relative form fails. |
| Browser scales the print to "fit" and shrinks cards | A4 + 8mm margin is well within A4 size; 2×85.6 + 4 = 175.2mm ≤ 210−16 = 194mm. No fit-shrink. Verify in Chrome and Edge. |
| Section with 100+ students spawns excessive print pages | 100 students = 10 A4 pages — acceptable. If pathological (500+), document a follow-up story for paginated print. |
| `StudentEnrollment` doesn't include `AcademicYear` navigation | Verify in `ApplicationDbContext` config. If only `AcademicYearId` is FK, query becomes `.Where(e => e.AcademicYear.IsCurrent)` after `.Include(e => e.AcademicYear)`. The example query already assumes nav property. |
| `QrCode.IsValid` filter inside `.Any()` triggers client-eval warning | Use `.Where(q => q.IsValid)` projection instead, or assert behaviour in test. |
| Page breaks split a card | `page-break-inside: avoid` on `.id-card` (already in CSS). Verified by Phase 6 smoke. |

---

## Open Questions

- **Per-student selection on the bulk page (checkbox each card)?** Out of scope; story explicitly defers. Note in revision if requested.
- **A4 landscape vs portrait?** Portrait fits 2×5 = 10 cards/page cleanly. Landscape would fit 3×3 = 9, less efficient. Stick with portrait.
- **Cut guide hairlines between cards?** AC3 mentions them; the current CSS uses `gap: 4mm` whitespace. If actual hairlines are wanted, add `border: 0.1mm dashed #ccc` to `.id-card` — easy follow-up, not blocking.

---

## Done Definition

- [ ] All Phase 1–7 tasks checked off
- [ ] AC1–AC10 verified by smoke + tests
- [ ] `dotnet build` clean; `dotnet test` passes
- [ ] US0113 status → Review
- [ ] US0022 has "Superseded" note appended

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude (Opus 4.7) | Initial plan drafted |
