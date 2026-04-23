# US0064: Student Program Denormalization

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** system
**I want** the student record to carry a denormalized Program field
**So that** queries, reports, and display don't need to join through Section → Program every time

## Context

### Background
Student.Program stores the leaf program code from the student's current section. It's auto-set on enrollment and updated on re-enrollment. This simplifies attendance queries, report filtering, and UI display.

---

## Acceptance Criteria

### AC1: Student Entity Field
- **Given** the Student entity
- **Then** a new field `Program` (string?, max 20, nullable) exists

### AC2: Auto-Set on Enrollment
- **Given** a student is enrolled in section "Ruby" which belongs to program "STE"
- **When** the enrollment is saved
- **Then** `Student.Program` is set to "STE"

### AC3: Auto-Set on Re-enrollment
- **Given** a student currently has `Program = "STE"` (Grade 10)
- **When** the student is re-enrolled to Grade 11, section "Einstein" (Program: STEM)
- **Then** `Student.Program` is updated to "STEM"

### AC4: Display on Student List
- **Given** the student list page
- **Then** a "Program" column is visible showing the denormalized program code

### AC5: Display on Student Detail
- **Given** I view a student's detail page
- **Then** Program is shown alongside Grade Level and Section

### AC6: Migration Default
- **Given** existing students with no Program value
- **When** the migration runs
- **Then** `Student.Program` is set based on their current section's program
- **And** students with no active enrollment get `Program = null`

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student not enrolled in any section | Program = null |
| Section's program changed after enrollment | Student.Program NOT auto-updated (snapshot) |
| Bulk import with Program column | Set directly from import |
| Student transferred between sections | Program updated to new section's program |

---

## Test Scenarios

- [ ] Program field exists on Student entity
- [ ] Auto-set when student enrolled
- [ ] Updated on re-enrollment
- [ ] Displayed on student list
- [ ] Displayed on student detail
- [ ] Migration sets from current enrollment
- [ ] Null for unenrolled students

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program entity | Draft |
| [US0060](US0060-section-program-mandatory.md) | Data | Sections have programs | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
