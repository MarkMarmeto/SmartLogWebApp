# US0035: Class Attendance View

> **Status:** Done
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Teacher Tina (Teacher)
**I want** to view attendance for my class
**So that** I can quickly see which students are present without manual roll call

## Context

### Persona Reference
**Teacher Tina** - Classroom teacher who tracks her students' attendance.
[Full persona details](../personas.md#3-teacher-tina-classroom-teacher)

---

## Acceptance Criteria

### AC1: Class Selection
- **Given** I am logged in as Teacher Tina
- **When** I navigate to Attendance
- **Then** I see a dropdown to select Grade and Section
- **And** I can select my class (e.g., "Grade 5 - Section A")

### AC2: Class Attendance Summary
- **Given** I select "Grade 5 - Section A"
- **Then** I see summary:
  - Class Size: 30
  - Present: 28
  - Absent: 2
  - Attendance Rate: 93%

### AC3: Class Student List
- **Given** I am viewing class attendance
- **Then** I see a list of students in my class with:
  - Name
  - Student ID
  - Status (Present / Absent / Departed)
  - Entry Time

### AC4: Absent Students Highlighted
- **Given** the class attendance list
- **Then** absent students are shown at the top (or highlighted)
- **And** I can quickly identify who is missing

### AC5: Read-Only View
- **Given** I am viewing class attendance as Teacher
- **Then** I cannot modify attendance records
- **And** I cannot mark students present/absent manually

### AC6: Quick Class Switch
- **Given** I teach multiple classes
- **When** I change the Grade/Section filter
- **Then** the attendance updates without page reload
- **And** I can quickly check multiple classes

### AC7: Simpler View Than Admin
- **Given** I am a Teacher
- **Then** I see a simpler interface than Admin
- **And** I do NOT see school-wide statistics
- **And** focus is on my selected class only

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No class selected | Prompt to select Grade/Section |
| Empty class (no students) | Show "No students in this class" |
| All students absent | Show all absent, 0% attendance |
| Teacher views wrong class | Allow (no restriction on class view for now) |
| Network error | Show error, preserve selection |
| Student transferred mid-day | Show based on current class assignment |

---

## Test Scenarios

- [ ] Grade/Section dropdowns populated
- [ ] Class summary shows correct counts
- [ ] Student list shows class members only
- [ ] Absent students highlighted or at top
- [ ] Entry times displayed correctly
- [ ] Cannot modify attendance (read-only)
- [ ] Quick switch between classes works
- [ ] Empty class handled gracefully
- [ ] Only active students shown
- [ ] Teacher cannot see admin dashboard

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0034](US0034-attendance-dashboard.md) | Shared logic | Attendance queries | Draft |
| [US0018](US0018-student-list.md) | Functional | Student Grade/Section data | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Teacher role check | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
