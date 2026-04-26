# US0104: Section Create/Edit — Hide Program Dropdown for Non-Graded

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0028: Section Create/Edit — Hide Program Dropdown for Non-Graded](../plans/PL0028-section-ui-hide-program-for-ng.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26

## User Story

**As a** Admin Amy (Administrator)
**I want** the Program dropdown to disappear from the Section form when I select the Non-Graded grade level
**So that** I'm not forced to pick a Program that doesn't apply to NG learners.

## Context

### Persona Reference
**Admin Amy** — creates and maintains sections each academic year.

### Background
With `Section.ProgramId` nullable (US0103), the form now needs to reflect the rule "ProgramId required unless GradeLevel = NG". The current Section create/edit pages always render a required Program dropdown. This story makes the dropdown conditional on the selected Grade Level and clears the field appropriately when the user toggles grade levels.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0103 | Data | ProgramId is nullable; required for non-NG | Form must enforce on submit + reflect server validation |
| EP0010 | UX | Section edit shouldn't surprise users with hidden fields | Switching grade level visibly toggles the Program field with a hint |

---

## Acceptance Criteria

### AC1: Hide Program Dropdown for NG
- **Given** I am on `/Admin/CreateSection` or `/Admin/EditSection/{id}`
- **When** I select Grade Level = "Non-Graded"
- **Then** the Program dropdown is hidden
- **And** a small inline note appears: "Non-Graded sections do not use a Program"
- **And** the form-level required marker on Program is removed

### AC2: Show Program Dropdown for Graded Levels
- **Given** I select any Grade Level other than NG
- **Then** the Program dropdown is visible and required
- **And** it is filtered by the selected Grade Level via `GradeLevelProgram` (existing behaviour, unchanged)

### AC3: Switching from NG to Graded Clears State Correctly
- **Given** I had Grade Level = NG (Program hidden)
- **When** I switch Grade Level to Grade 7
- **Then** the Program dropdown becomes visible with no Program pre-selected
- **And** I must pick a Program before submit

### AC4: Switching from Graded to NG Clears Program
- **Given** I had Grade Level = Grade 7 with Program = STEM selected
- **When** I switch Grade Level to NG
- **Then** the Program selection is cleared (set to null)
- **And** the dropdown is hidden
- **And** submitting saves the section with `ProgramId = null`

### AC5: Server-Side Validation on Submit
- **Given** I tamper with the form to submit a Graded section without a Program
- **Then** the server-side validation (US0103) rejects the save and shows the error against the Program field

### AC6: Edit Page Initial Render
- **Given** I open `/Admin/EditSection/{id}` for an existing NG section (Program null)
- **Then** Grade Level shows "Non-Graded" pre-selected
- **And** the Program dropdown is hidden by default
- **And** no validation error shows on first render

---

## Scope

### In Scope
- `CreateSection.cshtml(.cs)` — conditional render + JS toggle
- `EditSection.cshtml(.cs)` — same
- Razor `[BindProperty]` model — `ProgramId` becomes `Guid?` to bind null cleanly
- Inline JS for show/hide (vanilla — match existing page pattern)

### Out of Scope
- Bulk section creation flows (none currently exist for sections)
- Section list page filters
- Server-side validation rule (delivered in US0103)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| User has JS disabled | Server-side validation still enforces; form just always shows the dropdown |
| User picks NG but the form was previously Grade 7 with stale Program in `ProgramId` hidden field | JS clears the hidden field on toggle to NG |
| Section already has students enrolled and admin tries to switch its Grade Level | Out of scope here — separate Section move flow; this story only handles the NG vs graded toggle |

---

## Test Scenarios

- [ ] Selecting NG hides Program dropdown and the inline note is shown
- [ ] Selecting Grade 7 shows Program dropdown filtered by GradeLevelProgram
- [ ] Switching NG → Grade 7 reveals the dropdown empty
- [ ] Switching Grade 7 (with Program selected) → NG clears ProgramId on submit
- [ ] EditSection for an existing NG section renders correctly on first load
- [ ] Submitting a tampered Graded section without ProgramId returns server validation error

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/CreateSection.cshtml(.cs)`
- **Modify:** `src/SmartLog.Web/Pages/Admin/EditSection.cshtml(.cs)`
- **Modify (PageModel):** `[BindProperty] public Guid? ProgramId { get; set; }`
- **JS:** inline `<script>` block that listens to GradeLevel `change` and toggles the Program section's `hidden` + clears value when target is NG. Identify NG by GradeLevel `Code === "NG"` (passed via `data-` attribute on the option).

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0103](US0103-section-programid-nullable.md) | Data | ProgramId nullable + validation rule | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low — two pages, conditional render + small JS.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
