# US0109: Student Details, List & ID Card — Non-Graded Display

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0033: Student Details, List & Card — Non-Graded Display](../plans/PL0033-student-details-card-ng-display.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26
> **Marked Done:** 2026-04-26

## User Story

**As a** Admin Amy (Administrator) / Teacher Tina
**I want** Student Details, the Student List, and the printed ID card to render Non-Graded students cleanly without showing a Program code
**So that** the UI and physical artifacts reflect that NG learners have no Program assignment.

## Context

### Persona Reference
**Admin Amy** — manages student records and prints cards. **Teacher Tina** — looks up students.

### Background
US0087 (Student Details — Display Program Code) assumes every student has a Program. NG students don't. EP0013 (QR Card Redesign) is Done and renders Program on the front of the card. Both surfaces must gracefully handle the NG case: show "Non-Graded" where appropriate, omit the Program line entirely on the card front.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0106 | Data | Student.Program is null for NG students | All consumers must handle null |
| US0087 | Predecessor | Details/List currently shows Program | Add NG branch |
| EP0013 | Predecessor | Card front renders Program | Card must omit Program line for NG |

---

## Acceptance Criteria

### AC1: Student Details Header for NG
- **Given** I view `/Admin/Students/Details/{id}` for an NG student
- **Then** the header reads "Non-Graded · Section: LEVEL 2" (no Program token)
- **And** the format for graded students is unchanged: "Grade 11 — STEM · Section: STEM-A"

### AC2: Student Details Info Card for NG
- **Given** the info card on the details page
- **Then** the Program row shows "—" (or is omitted entirely — pick one consistent treatment; spec: show "—" so the row order stays stable)
- **And** the Grade Level row shows "Non-Graded"

### AC3: Student List Program Column for NG
- **Given** I view `/Admin/Students` with NG students in the list
- **Then** the Program column shows "—" for NG rows
- **And** sorting by Program places NG rows together (either at the start or end consistently)

### AC4: CSV Export for NG
- **Given** I export the Student list to CSV
- **Then** NG rows have an empty string in the Program column

### AC5: ID Card (CR80) — NG Variant
- **Given** I print the ID card for an NG student
- **Then** the front of the card omits the Program line entirely
- **And** the Grade line reads "Non-Graded" (instead of "Grade 11")
- **And** layout reflows so the Section line moves up to fill the gap (no awkward empty space)

### AC6: Enrollment Sticker for NG — SUPERSEDED
- Sticker feature removed by US0110. AC6 no longer applies.

### AC7: Inactive Program Badge Doesn't Apply to NG
- **Given** an NG student
- **Then** no "Inactive program" badge appears (there is no Program)

---

## Scope

### In Scope
- Student Details page (`Pages/Admin/Students/Details.cshtml` — or current path)
- Student List page (`Pages/Admin/Students.cshtml`)
- Student CSV export
- ID Card template (front)

### Out of Scope
- Re-doing Inactive program badge logic for graded
- Reports filters/columns (covered by US0108)
- Broadcast UI (covered by US0107)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Legacy NG student where `Student.Program` was previously set to "REGULAR" | After US0106 batch normalisation, Program is null; until then, display logic should also treat Program as null when the section's GradeLevel is NG (defensive) |
| Card front overflow when long Section name on a graded student | Existing behaviour unchanged |

---

## Test Scenarios

- [ ] Details header for NG omits Program; reads "Non-Graded · Section: LEVEL 2"
- [ ] Details info card Program row shows "—"; Grade Level shows "Non-Graded"
- [ ] List Program column shows "—" for NG students; CSV exports empty
- [ ] Card front for NG: Grade = "Non-Graded", no Program line, layout reflows
- [ ] Graded student rendering unchanged (regression)

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Details.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students.cshtml(.cs)`
- **Modify:** student CSV export service
- **Modify:** ID card Razor partial (locate via EP0013 — likely `Pages/Admin/IdCard*.cshtml` or `Templates/IdCardFront.cshtml`)

### Display Helper
A small extension method keeps templates clean:
```csharp
public static string ProgramDisplay(this Student s) => s.Program ?? "—";
public static bool IsNonGraded(this Student s) => s.Section?.GradeLevel?.Code == "NG";
```

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0087](US0087-student-details-program-code-display.md) | Predecessor | Details/List Program column | Draft |
| [US0106](US0106-student-program-null-for-ng.md) | Data | Student.Program null for NG | Draft |
| [US0078](US0078-card-template.md) | Predecessor | CR80 card template (EP0013) | Done |

---

## Estimation

**Story Points:** 3
**Complexity:** Low-Medium — display-only across several surfaces; card layout reflow is the only mildly tricky bit.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
