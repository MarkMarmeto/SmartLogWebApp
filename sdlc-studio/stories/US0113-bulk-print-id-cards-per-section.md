# US0113: Bulk Print ID Cards per Section

> **Status:** Review
> **Plan:** [PL0036: Bulk Print ID Cards per Section](../plans/PL0036-bulk-print-id-cards-per-section.md)
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-27
> **Supersedes:** [US0022: Bulk Print QR Codes](US0022-bulk-print-qr.md)

## User Story

**As a** Admin Amy (Administrator)
**I want** to print all student ID cards for a given section in one go
**So that** I can prepare cards for an entire class at the start of the year (or for new enrollees) without printing one card at a time

## Context

### Background

US0022 was marked Done but no bulk-print page exists in the codebase — the feature was never actually shipped. Today, the only print path is single-student via `/Admin/PrintQrCode/{id}`, which forces admins to open and print each student individually. For a 40-student section, that's ~40 separate print dialogs.

This story adds a section-scoped bulk print page that arranges multiple CR80 ID cards on a single A4 sheet for batch printing and cutting. It uses the **same card design as US0112** so visual consistency is automatic — just tiled.

This story depends on US0112 (the card layout component) and US0111 (the school branding settings).

### Why per Section (not per Grade)
Sections are the natural printing unit: an Adviser distributes cards to her own class, prints are batched per Adviser, and the Section list is already a routine admin filter. Per-grade or all-school bulk would also be useful but is out of scope here — extension story if needed.

---

## Acceptance Criteria

### AC1: Entry Point on Section List
- **Given** I am on `/Admin/Sections` (or the Section detail view)
- **When** I view a section row
- **Then** I see a "Print ID Cards" action that links to `/Admin/PrintIdCards/Section/{sectionId}`

### AC2: Bulk Page — Active Enrolled Students
- **Given** Section "Grade 7 — St. Augustine" has 35 active enrollments in the **current academic year** and 2 inactive ones
- **When** I open `/Admin/PrintIdCards/Section/{id}`
- **Then** the page shows exactly 35 ID cards (one per active enrollment)
- **And** inactive enrollments are excluded
- **And** students without a valid QR code are excluded with a visible warning at the top: "X student(s) skipped — no valid QR. [list with regenerate links]"

### AC3: A4 Sheet Layout
- **Given** the bulk page rendered for 35 students
- **Then** cards are arranged in a **2×5 grid per A4 page** (10 cards per page) at exact CR80 dimensions (85.6mm × 54mm each)
- **And** there are visible cut guides (light hairlines) between cards
- **And** total of 4 A4 pages render for 35 students (10 + 10 + 10 + 5)

### AC4: Card Visual Parity with Single Print
- **Given** any card on the bulk page
- **Then** it is **visually identical** to the single-student card from US0112 (same header, same left/right split, same QR size)
- **And** it uses the same school logo + school name from `AppSettings`

### AC5: Print Controls Hidden in Print
- **Given** I click the Print button
- **Then** the browser print dialog shows only the card grid
- **And** screen-only controls (Print / Close, the warnings banner, the section title) do not appear in the printed output

### AC6: Section Header on Screen Only
- **Given** I open the bulk page
- **Then** above the grid, I see a screen-only summary: "Grade 7 — St. Augustine • 35 students"
- **And** this summary is hidden when printing

### AC7: Authorization
- **Given** the route requires `CanManageStudents` (matches single Print policy uplift, since bulk is an admin operation)
- **When** a Teacher / Security / Staff user opens the URL directly
- **Then** they receive 403 / Access Denied

### AC8: Section Not Found
- **Given** I open `/Admin/PrintIdCards/Section/{nonexistent-guid}`
- **Then** the page returns 404

### AC9: Empty Section
- **Given** a section with zero active enrollments in the current AY
- **Then** the page shows "No students to print for this section" and no print button

### AC10: Performance — Reasonable Sections
- **Given** a typical section size (≤ 50 students)
- **Then** the page renders in ≤ 3 seconds on the production server
- **And** no per-card N+1 queries occur (single query with `.Include` for enrollments + students + QrCodes)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Section has no enrollments at all | "No students to print", print button disabled |
| Some students missing photos | Cards render with initials placeholder (per US0112 AC8) |
| Some students missing QR | Excluded from grid, listed in screen-only warning with link to regenerate |
| Student with very long name | Truncated per US0112 AC9 — no layout overflow |
| Print stretches across many pages | CSS `page-break-inside: avoid` on each card so no card splits across pages |
| Browser zoom set to non-100% | Card dimensions remain exact (mm units) — only screen preview scales |

---

## Test Scenarios

- [ ] Entry point visible on Section list / detail
- [ ] Bulk page returns only active enrollments for the current AY
- [ ] Inactive enrollments excluded
- [ ] Students without valid QR excluded + listed in warning
- [ ] 2×5 grid renders at 10 cards per A4 page
- [ ] Card layout identical to single-print version
- [ ] Print hides screen controls and warning banner
- [ ] Empty section shows friendly message
- [ ] Non-existent section returns 404
- [ ] Teacher role denied
- [ ] No N+1 queries in EF logs for a 50-student section
- [ ] Each card stays on a single page (no mid-card page breaks)

---

## Technical Notes

### New Files
- `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml` — bulk print page
- `src/SmartLog.Web/Pages/Admin/PrintIdCards.cshtml.cs` — handler with `OnGetSectionAsync(Guid id)`

### Why a Section route, not a query parameter
A route like `/Admin/PrintIdCards/Section/{id}` keeps it open to a future `/Admin/PrintIdCards/Grade/{id}` or `/Admin/PrintIdCards/Custom?ids=...` without ambiguous parameters. Out of scope for this story but cheap to leave room for.

### Card Markup Reuse
Extract the card markup from `PrintQrCode.cshtml` (post-US0112) into a Razor partial `_StudentIdCard.cshtml` that takes `(Student, QrCode, BrandingSettings)`. Both the single and bulk pages render the partial. This avoids two copies of the layout drifting apart.

### Query Shape
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

### Print CSS
```css
@media print {
    @page { size: A4; margin: 8mm; }
    .card-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 4mm; }
    .id-card { width: 85.6mm; height: 54mm; page-break-inside: avoid; }
}
```

### What this story does NOT do
- No PDF generation (US0022 mentioned QuestPDF) — browser print is sufficient and zero-dependency.
- No per-student selection UI on the bulk page — print all active in section. Selection is a future story.
- No print history / job tracking.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0112](US0112-id-card-landscape-redesign.md) | Functional | New card layout, Razor partial | Draft |
| [US0111](US0111-school-branding-settings.md) | Functional | School logo + name available | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium — straightforward once US0112's partial exists; main work is the section query, A4 grid CSS, and warning-banner UX.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft — browser-print bulk per section, supersedes US0022 |
