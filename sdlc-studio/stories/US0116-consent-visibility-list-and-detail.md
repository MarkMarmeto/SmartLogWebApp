# US0116: Consent Visibility on Student List & Detail

> **Status:** Draft
> **Epic:** [EP0016: PII & RA 10173 Compliance — Consent & Notice (Floor)](../epics/EP0016-pii-ra10173-compliance.md)
> **Owner:** TBD
> **Created:** 2026-04-27

## User Story

**As an** Admin Amy (Administrator)
**I want** to see which students have given data-processing consent at a glance and filter the list to find students who have not
**So that** I can chase up missing parental consent forms and demonstrate RA 10173 coverage to school leadership without opening every student record.

## Context

### Persona Reference
**Admin Amy** — primary user; uses the filter weekly to track consent backfill progress during the rollout.
**SuperAdmin Tony** — uses the same view to spot-check consent coverage as a compliance metric.

### Background
US0115 lets admins record consent. This story makes the recorded value discoverable from the existing student list and detail pages so the data is actually usable for the chase-up workflow. Without it, consent would be invisible unless an admin opened the Edit form for every student.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0016 | UX | Consent status must be visible without opening Edit | Surface on list and detail |
| EP0016 | Compliance | Admin needs to find students missing consent | Filter must support "Not given" |
| US0114 | Data | Consent fields exist on `Student` | List query reads the columns |
| TRD | UI | Razor Pages list with existing filter pattern | Reuse the existing filter pipeline (grade, section, status) |

---

## Acceptance Criteria

### AC1: List Column
- **Given** I navigate to `/Admin/Students`
- **Then** the table includes a "Consent" column positioned after "Status" (before "Actions")
- **And** each row shows a badge:
  - **Given** — green pill with the date, e.g. `Given · 2026-04-12`
  - **Not given** — grey pill: `Not given`

### AC2: Filter Control
- **Given** the student list page
- **Then** the existing filter row includes a "Consent" dropdown with three options:
  - `All` (default)
  - `Given`
  - `Not given`
- **And** changing the value re-queries with the filter applied
- **And** the filter value is preserved in the URL query string and survives pagination

### AC3: Filter Query Behaviour
- **Given** filter `Consent = Given`
- **Then** only students with `DataProcessingConsent = true` are returned
- **And** the filter combines correctly with existing grade/section/search filters (logical AND)

- **Given** filter `Consent = Not given`
- **Then** only students with `DataProcessingConsent = false` are returned

### AC4: Detail Page Badge
- **Given** I open `/Admin/Students/Details/{id}`
- **Then** I see a consent block in the "Data Privacy" section showing:
  - Badge (`Given` / `Not given`) matching the list style
  - If given: `Recorded on yyyy-MM-dd`
  - If not given: helper text `No consent recorded — capture via the Edit screen.`

### AC5: Sortability — Out of Scope (Explicit)
- The list is **not** sortable by the Consent column in this story. Filtering is sufficient for the chase-up workflow. Sorting can be added later if requested.

### AC6: Authorization Unchanged
- **Given** the existing `CanViewStudents` policy on the list and detail pages
- **Then** no policy changes are introduced
- **And** users who can see the student list can also see the consent column

### AC7: No Performance Regression
- **Given** the new filter and column
- **Then** the list page load time on a 2,000-student dataset is within ±10% of the baseline (no new joins; reads existing columns)

---

## Scope

### In Scope
- New "Consent" column on `Pages/Admin/Students/Index.cshtml`
- New filter dropdown bound to a nullable `bool? ConsentFilter` in the page model
- Query update to apply the filter
- Detail page block on `Pages/Admin/Students/Details.cshtml`
- Reuse existing badge / pill CSS classes (no new CSS unless none exist)

### Out of Scope
- Sortability by consent column
- Bulk-set consent action
- Export-to-CSV inclusion of consent (can be added later if requested)
- Withdrawal cascade
- Audit log viewer changes (US0050 already shows all AuditLog actions including the new `Student.ConsentUpdated`)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student has `DataProcessingConsent = true` but `ConsentDate = null` (legacy data only — should not occur post-US0115) | List shows `Given · —`; detail shows `Recorded on (date unknown)`; no error |
| Filter `Not given` returns thousands of pre-existing students | Page renders with pagination; the count is informative, not an error |
| Search + filter + grade combination returns zero rows | Existing empty-state message shown |

---

## Test Scenarios

- [ ] List page renders the Consent column for all rows
- [ ] Filter `Given` returns only consented students
- [ ] Filter `Not given` returns only non-consented students
- [ ] Filter combines correctly with grade + search
- [ ] Filter value is preserved through pagination links
- [ ] Detail page shows the badge and date when consent is given
- [ ] Detail page shows the not-given badge + helper text when consent is missing

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Index.cshtml(.cs)` — column, filter dropdown, query
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Details.cshtml` — consent block

### Query Approach
Append to the existing IQueryable filter chain:
```csharp
if (ConsentFilter is true)  query = query.Where(s => s.DataProcessingConsent);
if (ConsentFilter is false) query = query.Where(s => !s.DataProcessingConsent);
```
Bind the dropdown to a tri-state model: `null` (All) / `true` (Given) / `false` (Not given).

### Badge Styling
Reuse whatever pill / badge utility classes the existing list uses for `Status` (Active / Inactive). If none, add minimal scoped CSS in the page section.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0114](US0114-student-consent-fields.md) | Predecessor | Consent fields exist | Draft |
| [US0115](US0115-consent-capture-on-student-form.md) | Co-requisite | Admins can actually populate the field; without this, the column is always "Not given" | Draft |

### Blocks

- None within EP0016. (Future Subject-Rights Epic may add a "Withdraw consent" action button reusing this column.)

---

## Estimation

**Story Points:** 2
**Complexity:** Low — read-only column + nullable bool filter; reuses existing list infrastructure.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-27 | Claude | Initial draft as part of EP0016 V2.1 activation (consent + notice floor) |
