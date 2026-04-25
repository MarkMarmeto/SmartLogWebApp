# US0045: Daily Attendance Report

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to generate a daily attendance report
**So that** I can review attendance for a specific day and share with stakeholders

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who prepares attendance reports.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Daily Report Page
- **Given** I am logged in as Admin
- **When** I navigate to Reports > Daily Attendance
- **Then** I see a report configuration form with:
  - Date picker (default: today)
  - Grade filter (optional)
  - Section filter (optional)
  - Generate Report button

### AC2: Report Content
- **Given** I generate a daily report for February 4, 2026
- **Then** the report shows:
  - Report Header: "Daily Attendance Report - February 4, 2026"
  - School Name
  - Generated timestamp
  - Summary section
  - Student detail table

### AC3: Report Summary Section
- **Given** the daily report is generated
- **Then** the summary shows:
  - Total Enrolled: 500
  - Present: 485 (97.0%)
  - Absent: 12 (2.4%)
  - Late: 3 (0.6%)
  - Departed Early: 5

### AC4: Student Detail Table
- **Given** the daily report is generated
- **Then** the student table includes:
  | Student ID | Name | Grade | Section | Status | Entry Time | Exit Time |
  |------------|------|-------|---------|--------|------------|-----------|
  | STU-2026-001 | Maria Santos | 5 | A | Present | 7:45 AM | 4:30 PM |
  | STU-2026-002 | Juan Reyes | 5 | A | Late | 8:15 AM | 4:30 PM |
  | STU-2026-003 | Ana Cruz | 5 | A | Absent | - | - |

### AC5: Define "Late" Arrival
- **Given** school start time is configured as 7:30 AM
- **When** a student's entry scan is after 7:30 AM
- **Then** they are marked as "Late" in the report

### AC6: Filter by Grade/Section
- **Given** I select Grade "5" and Section "A"
- **When** I generate the report
- **Then** only Grade 5, Section A students are included
- **And** summary reflects filtered data

### AC7: Sort Options
- **Given** the report is generated
- **Then** I can sort by:
  - Name (A-Z)
  - Status (Absent first)
  - Entry Time (earliest first)

### AC8: Report Performance
- **Given** the school has 2000 students
- **Then** the report generates within 30 seconds

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No students enrolled | Show "No students to report" |
| Future date selected | Disabled or show warning |
| Date before system launch | Show "No data available" |
| Student transferred mid-day | Show based on AM class |
| Multiple entry scans | Use first entry of the day |
| Very large school | Pagination option |

---

## Test Scenarios

- [ ] Report page loads correctly
- [ ] Date picker defaults to today
- [ ] Report generates successfully
- [ ] Summary numbers are accurate
- [ ] Student table shows all students
- [ ] Late status calculated correctly
- [ ] Filter by Grade works
- [ ] Filter by Section works
- [ ] Sort by name works
- [ ] Sort by status works
- [ ] Report generates under 30 seconds
- [ ] Empty date shows appropriate message

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0034](US0034-attendance-dashboard.md) | Shared logic | Attendance queries | Draft |
| [US0015](US0015-create-student.md) | Functional | Students exist | Draft |
| [US0030](US0030-scan-ingestion-api.md) | Functional | Scan data exists | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
