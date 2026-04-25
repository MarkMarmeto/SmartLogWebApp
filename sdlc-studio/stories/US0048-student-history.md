# US0048: Student Attendance History

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to view an individual student's attendance history
**So that** I can discuss attendance with parents and identify patterns

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who prepares for parent-teacher conferences.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Access from Student Details
- **Given** I am viewing student "Maria Santos" details
- **When** I click "View Attendance History"
- **Then** I see the student's attendance history page

### AC2: Student Info Header
- **Given** I am viewing attendance history for Maria Santos
- **Then** I see:
  - Student Name: Maria Santos
  - Student ID: STU-2026-001
  - Grade/Section: Grade 5 - Section A
  - Enrollment Date: August 15, 2025

### AC3: Attendance Summary Card
- **Given** I view the attendance history
- **Then** I see current year summary:
  - School Days: 100
  - Days Present: 95
  - Days Absent: 5
  - Days Late: 3
  - Attendance Rate: 95%

### AC4: Monthly Calendar View
- **Given** I view the attendance history
- **Then** I see a calendar with color-coded days:
  - Green: Present (on time)
  - Light Green: Present (late)
  - Red: Absent
  - Gray: Weekend/Holiday
  - White: No school / Not enrolled

### AC5: Navigate Months
- **Given** I am viewing February 2026
- **When** I click previous/next month arrows
- **Then** the calendar updates to show that month

### AC6: Attendance Detail Table
- **Given** I view the attendance history
- **Then** I see a detailed table:
  | Date | Day | Status | Entry | Exit | Notes |
  |------|-----|--------|-------|------|-------|
  | Feb 4 | Tue | Present | 7:25 AM | 4:30 PM | - |
  | Feb 3 | Mon | Late | 7:45 AM | 4:30 PM | Arrived 15 min late |
  | Feb 2 | Sun | - | - | - | Weekend |
  | Jan 31 | Fri | Absent | - | - | - |

### AC7: Filter by Status
- **Given** the attendance history table
- **When** I filter by "Absent"
- **Then** I see only absent days
- **And** can identify absence patterns

### AC8: Print/Export Student History
- **Given** I am viewing a student's attendance history
- **When** I click "Print" or "Export PDF"
- **Then** a printable version is generated
- **And** suitable for sharing with parents

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| New student (no history) | Show "No attendance records yet" |
| Transferred student | Show history from all schools/classes |
| Multiple scans same day | Show first entry, last exit |
| Scan without exit | Show "No exit recorded" |
| Deactivated student | History still viewable |

---

## Test Scenarios

- [ ] Access from student details works
- [ ] Student info header correct
- [ ] Summary statistics accurate
- [ ] Calendar view displays correctly
- [ ] Color coding accurate
- [ ] Month navigation works
- [ ] Detail table shows all records
- [ ] Filter by status works
- [ ] Print/export works
- [ ] New student shows empty state
- [ ] Transferred student shows full history

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Functional | Student records | Draft |
| [US0030](US0030-scan-ingestion-api.md) | Functional | Scan history | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
