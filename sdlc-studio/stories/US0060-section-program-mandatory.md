# US0060: Section-Program Mandatory Linking

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** every section to be required to belong to a program
**So that** all students are properly categorized by program for reporting and communication

## Context

### Background
Currently Section.ProgramId is NULL for all sections. After this story, every section must have a ProgramId. Section create/edit forms show a program dropdown filtered by the section's grade level, showing only leaf programs.

---

## Acceptance Criteria

### AC1: Section Entity Changed
- **Given** the Section entity in the database schema
- **When** the migration is applied
- **Then** `ProgramId` (Guid, FK → Program) is REQUIRED (NOT NULL)
- **And** navigation property `Program` is required

### AC2: Section Create Form
- **Given** I am creating a new section
- **And** I select Grade "Grade 7"
- **Then** the Program dropdown shows only leaf programs linked to Grade 7:
  - REGULAR, SPA-VA, SPA-MUS, SPA-DNC, SPA-THTR, SPE, SPJ, STE
- **And** the dropdown is required (cannot submit without selection)

### AC3: Grade Change Updates Program Options
- **Given** I am creating a section and selected Grade "Grade 7" with Program "STE"
- **When** I change Grade to "Grade 11"
- **Then** the Program dropdown updates to show Grade 11 leaf programs:
  - REGULAR, STEM, ABM, HUMSS, GAS, SPORTS, ADT, TVL-HE-CK, TVL-HE-FBS, TVL-ICT-CP, TVL-ICT-CSS, TVL-IA-EIM, TVL-IA-SMAW, TVL-AFA
- **And** the previously selected "STE" is cleared

### AC4: Section Edit Form
- **Given** I am editing an existing section assigned to REGULAR
- **Then** the Program dropdown shows the current value selected
- **And** I can change the program to another leaf program for the same grade

### AC5: Section Display Format
- **Given** a section "Amethyst" in Grade 7, Program STE
- **Then** it displays as "Grade 7 - STE - Amethyst" wherever section is shown

### AC6: Non-Graded Sections
- **Given** I am creating a section for grade "Non-Graded"
- **Then** the Program dropdown shows REGULAR (and any custom programs linked to NG)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No leaf programs for selected grade | Show "No programs available — add programs first" |
| Program deactivated after section created | Section retains assignment; show warning on edit |
| Grade has no GradeLevelProgram links | Only REGULAR available (it's linked to all) |
| Section moved to different grade | Program dropdown refreshes; current program may become invalid |
| Bulk section creation | Same program requirement applies |

---

## Test Scenarios

- [ ] ProgramId is NOT NULL in database schema
- [ ] Section create form shows program dropdown
- [ ] Dropdown filtered by selected grade level
- [ ] Grade change clears and refreshes program options
- [ ] Section edit shows current program selected
- [ ] Cannot save section without program
- [ ] Display format "Grade - Program - Section" works
- [ ] Non-Graded sections can select REGULAR

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program entity exists | Draft |
| [US0059](US0059-seed-k12-programs-nongraded.md) | Data | Programs seeded | Draft |
| [US0065](US0065-programs-data-migration.md) | Migration | Existing sections assigned to REGULAR | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
