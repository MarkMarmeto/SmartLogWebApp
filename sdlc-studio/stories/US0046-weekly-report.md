# US0046: Weekly Attendance Summary

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to generate a weekly attendance summary report
**So that** I can review attendance trends over the week

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who tracks weekly attendance patterns.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Weekly Report Page
- **Given** I am logged in as Admin
- **When** I navigate to Reports > Weekly Summary
- **Then** I see a configuration form with:
  - Week picker (default: current week)
  - Grade filter (optional)
  - Section filter (optional)
  - Generate Report button

### AC2: Week Selection
- **Given** I am selecting a week
- **Then** the picker shows: "Week of Feb 3 - Feb 7, 2026"
- **And** I can navigate to previous/next weeks

### AC3: Report Summary Section
- **Given** I generate a weekly report
- **Then** the summary shows:
  - Week: February 3 - 7, 2026
  - School Days: 5
  - Average Daily Attendance: 96.5%
  - Total Absences: 75
  - Students with Perfect Attendance: 425

### AC4: Daily Breakdown Table
- **Given** the weekly report is generated
- **Then** I see a breakdown by day:
  | Day | Date | Enrolled | Present | Absent | Late | Rate |
  |-----|------|----------|---------|--------|------|------|
  | Mon | Feb 3 | 500 | 485 | 15 | 5 | 97.0% |
  | Tue | Feb 4 | 500 | 490 | 10 | 3 | 98.0% |
  | Wed | Feb 5 | 500 | 480 | 20 | 8 | 96.0% |
  | Thu | Feb 6 | 500 | 488 | 12 | 4 | 97.6% |
  | Fri | Feb 7 | 500 | 482 | 18 | 6 | 96.4% |

### AC5: Attendance Trend Chart
- **Given** the weekly report is generated
- **Then** I see a line chart showing:
  - X-axis: Days of the week
  - Y-axis: Attendance percentage
  - Visual trend line

### AC6: Students with Issues
- **Given** the weekly report is generated
- **Then** I see a section "Students Requiring Attention":
  - Students absent 2+ days
  - Students late 3+ times
  - Sorted by concern level

### AC7: Compare to Previous Week
- **Given** the weekly report is generated
- **Then** I see comparison to previous week:
  - "Attendance: 96.5% (↑ 0.5% from last week)"
  - "Absences: 75 (↓ 10 from last week)"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Incomplete week (e.g., holiday) | Show only school days |
| No school days in week | Show "No school days" |
| First week of system use | No comparison available |
| Student enrolled mid-week | Pro-rate calculations |
| Weekend selected | Skip weekends automatically |

---

## Test Scenarios

- [ ] Week picker works correctly
- [ ] Report generates successfully
- [ ] Daily breakdown is accurate
- [ ] Average attendance calculated correctly
- [ ] Perfect attendance count correct
- [ ] Trend chart displays
- [ ] Students with issues highlighted
- [ ] Previous week comparison works
- [ ] Filter by Grade works
- [ ] Holiday handling correct
- [ ] Report generates under 30 seconds

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0045](US0045-daily-report.md) | Shared logic | Daily calculations | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
