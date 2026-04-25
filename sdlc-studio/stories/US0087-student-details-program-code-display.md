# US0087: Student Details — Display Program Code with Grade & Section

> **Status:** Draft
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-24

## User Story

**As a** Admin Amy (Administrator) / Teacher Tina
**I want** the Student Details page to display the Program Code alongside the Grade and Section
**So that** I can tell at a glance which strand/program a student belongs to (e.g. "Grade 11 — STEM-A", "Grade 9 — BEC-Diamond") without drilling into Section details.

## Context

### Persona Reference
**Admin Amy** — reviews student profiles daily; **Teacher Tina** — looks up students in her class and others.

### Background
After EP0010, every `Section` has a mandatory `ProgramId`, and students are denormalised with `Student.Program` for fast listing. The Student Details view still renders Grade and Section as plain strings, omitting Program — a missing cue given that programs like STEM, ABM, BEC, TVE now drive cohort decisions, broadcasts, and reporting.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| EP0010 | Data | `Section.ProgramId` is mandatory; `Student.Program` is denormalised | Program is always available for display |
| PRD | UX | Program is a primary cohort concept post-EP0010 | Must be visible on the student's primary page |

---

## Acceptance Criteria

### AC1: Program Code in Header
- **Given** I view `/Admin/Students/Details/{id}` for an active student
- **Then** the header block shows Grade, Program Code, and Section in a clear, readable format
- **Example:** "Grade 11 — STEM · Section: STEM-A"

### AC2: Program Code in Info Card
- **Given** the student profile info card on the details page
- **Then** a dedicated "Program" row appears alongside Grade Level, Section, LRN, etc.
- **And** the Program row shows `{ProgramCode} — {ProgramName}` (e.g. "STEM — Science, Technology, Engineering, and Mathematics")

### AC3: Program Shown for REGULAR Sections
- **Given** a student assigned to a section with program `REGULAR`
- **Then** the program row renders "REGULAR — Regular" (or configured display name)
- **And** the header format gracefully includes it: "Grade 7 — REGULAR · Section: 7-A"

### AC4: Student List Also Shows Program
- **Given** I view the Student list (`/Admin/Students`)
- **Then** the list includes a Program column between Grade and Section
- **And** the column is sortable/filterable

### AC5: Print/Export Surfaces Reflect Program
- **Given** I print or CSV-export a student's details or a student list
- **Then** the Program Code is included alongside Grade and Section

---

## Scope

### In Scope
- Student Details page header + info card — add Program
- Student List page — add Program column
- Student print/export partials — include Program

### Out of Scope
- Editing Program directly from Student Details (Program is derived from Section; edit via Section assignment)
- Restructuring the overall layout of the details page

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student has `Program` denormalised but Section's program has since changed | Display denormalised value (fast path); a later sync story can reconcile — out of scope here |
| Program marked inactive after assignment | Still display code; badge "Inactive" next to the program name |
| Legacy student with null `Program` field | Fallback to resolving via `Section → Program` once, then cache for render |

---

## Test Scenarios

- [ ] Details page header includes Program Code
- [ ] Details page info card has a Program row
- [ ] REGULAR program displays correctly
- [ ] Student list has a Program column
- [ ] CSV export includes Program column
- [ ] Inactive program shows an "Inactive" badge

---

## Technical Notes

### Files to Modify
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Details.cshtml` — header + info card
- **Modify:** `src/SmartLog.Web/Pages/Admin/Students/Index.cshtml(.cs)` — list column + sort
- **Modify:** `src/SmartLog.Web/Services/StudentExportService.cs` (or equivalent) — CSV/print export

### Data Access
- Prefer the denormalised `Student.Program` field; fall back to `Student.Section.Program.Code` if null.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Data | Program entity exists | Done |
| [US0060](US0060-section-program-mandatory.md) | Data | Section has mandatory ProgramId | Done |
| [US0064](US0064-student-program-denormalization.md) | Data | Student.Program denormalised | Done |

---

## Estimation

**Story Points:** 2
**Complexity:** Low — display-only change across a small set of pages and the export

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-24 | Claude | Initial story drafted from V2 planning session |
