# US0034: School-Wide Attendance Dashboard

> **Status:** Done
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to view a real-time school-wide attendance dashboard
**So that** I can monitor student presence and respond to attendance issues

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who monitors daily attendance.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Dashboard Summary Cards
- **Given** I am logged in as Admin Amy
- **When** I navigate to Attendance Dashboard
- **Then** I see summary cards showing:
  - Total Enrolled Students (active students)
  - Present (students with ENTRY scan today)
  - Absent (no scans today)
  - Departed (students with EXIT scan after ENTRY)

### AC2: Attendance Statistics
- **Given** I am viewing the dashboard
- **Then** I see attendance rate as percentage
- **And** example: "Present: 485 / 500 (97%)"

### AC3: Student Attendance List
- **Given** I am viewing the dashboard
- **Then** I see a table of students with columns:
  - Student ID
  - Name
  - Grade / Section
  - Status (Present / Absent / Departed)
  - Entry Time (if present)
  - Exit Time (if departed)

### AC4: Status Visual Indicators
- **Given** the student list
- **Then** status is color-coded:
  - Present: Green badge
  - Absent: Red badge
  - Departed: Gray badge

### AC5: Default View Today
- **Given** I open the dashboard
- **Then** it shows today's attendance by default
- **And** the date is displayed: "Attendance for February 4, 2026"

### AC6: Dashboard Performance
- **Given** the school has 1000+ students
- **Then** the dashboard loads within 2 seconds
- **And** pagination is used (50 students per page)

### AC7: Last Updated Timestamp
- **Given** I am viewing the dashboard
- **Then** I see "Last updated: 8:15:30 AM"
- **And** the timestamp updates on each refresh

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No students enrolled | Show "No students enrolled" |
| No scans today (holiday) | Show all as Absent, 0% present |
| Weekend/no school | Dashboard still accessible |
| Student with multiple entries | Show most recent entry time |
| Database query timeout | Show error, retry option |
| Very large school (5000+) | Pagination, optimized queries |

---

## Test Scenarios

- [ ] Summary cards show correct counts
- [ ] Attendance percentage calculated correctly
- [ ] Student list displays with pagination
- [ ] Status colors correct (Present/Absent/Departed)
- [ ] Entry and exit times displayed correctly
- [ ] Default date is today
- [ ] Dashboard loads under 2 seconds
- [ ] Last updated timestamp shown
- [ ] Empty state handled gracefully
- [ ] Only active students included in counts

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0030](US0030-scan-ingestion-api.md) | Functional | Scan data exists | Draft |
| [US0015](US0015-create-student.md) | Functional | Students exist | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Admin role check | Draft |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium-High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
