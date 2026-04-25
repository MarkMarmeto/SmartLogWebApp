# US0063: Attendance & Report Program Filter

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to filter attendance views and reports by program
**So that** I can analyze attendance patterns per program group

## Context

### Background
With programs on sections, attendance data can be sliced by program. This adds a program filter to the dashboard, attendance API, and all report pages/exports.

---

## Acceptance Criteria

### AC1: Dashboard Program Filter
- **Given** I am on the Attendance Dashboard
- **And** I select Grade "Grade 11"
- **Then** a Program filter dropdown appears with leaf programs for Grade 11
- **And** "All Programs" is the default

### AC2: Attendance API Filter
- **Given** the attendance API endpoint `GET /api/v1/attendance/list`
- **Then** a new query parameter `?program=STEM` is supported
- **And** results are filtered to students in STEM sections

### AC3: Daily Report Program Filter
- **Given** I am on the Daily Report page
- **Then** a Program dropdown is available alongside Grade and Section
- **And** selecting a program filters the report

### AC4: Report Export Includes Program
- **Given** I export a daily report CSV with program filter "STEM"
- **Then** the CSV includes a "Program" column
- **And** only STEM students are included

### AC5: Report Grouping
- **Given** I view a monthly summary report for Grade 11
- **Then** the summary groups students by program within the grade:
  - STEM: 45 students, 92% avg attendance
  - ABM: 38 students, 89% avg attendance
  - ...

### AC6: Student.Program Used for Filtering
- **Given** a student enrolled in section "Ruby" (Program: STE, Grade: 7)
- **When** I filter the attendance API by `?program=STE`
- **Then** the student appears in results because `Student.Program` = "STE"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Program filter with section filter | Both apply (AND logic) |
| Grade with only REGULAR program | Dropdown shows only REGULAR |
| Student.Program is null (pre-migration) | Include in "REGULAR" group |
| Non-Graded program filter | Shows NG students |
| API with invalid program code | Return empty results (no error) |

---

## Test Scenarios

- [ ] Dashboard program dropdown appears after grade selection
- [ ] API accepts ?program= parameter
- [ ] API returns filtered results
- [ ] Daily report has program filter
- [ ] Report export includes Program column
- [ ] Monthly summary groups by program
- [ ] Student.Program auto-set on enrollment
- [ ] Combined grade + program + section filters work

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0058](US0058-program-entity-hierarchy.md) | Schema | Program entity | Draft |
| [US0060](US0060-section-program-mandatory.md) | Data | Sections have programs | Draft |
| [US0064](US0064-student-program-denormalization.md) | Data | Student.Program field | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
