# US0047: Monthly Attendance Report

> **Status:** Done
> **Epic:** [EP0008: Reporting & Analytics](../epics/EP0008-reporting-analytics.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to generate a monthly attendance report
**So that** I can submit compliance reports and review long-term trends

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who prepares monthly reports for school administration.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Monthly Report Page
- **Given** I am logged in as Admin
- **When** I navigate to Reports > Monthly Report
- **Then** I see a configuration form with:
  - Month/Year picker (default: current month)
  - Grade filter (optional)
  - Include weekends toggle (default: OFF)
  - Generate Report button

### AC2: Report Summary Section
- **Given** I generate a monthly report for February 2026
- **Then** the summary shows:
  - Month: February 2026
  - School Days: 20
  - Total Student-Days: 10,000 (500 students × 20 days)
  - Present Days: 9,650
  - Absent Days: 350
  - Overall Attendance Rate: 96.5%

### AC3: Weekly Breakdown
- **Given** the monthly report is generated
- **Then** I see weekly summaries:
  | Week | Dates | Avg Attendance | Absences |
  |------|-------|----------------|----------|
  | Week 1 | Feb 3-7 | 97.0% | 75 |
  | Week 2 | Feb 10-14 | 96.5% | 88 |
  | Week 3 | Feb 17-21 | 95.8% | 105 |
  | Week 4 | Feb 24-28 | 96.2% | 82 |

### AC4: Attendance by Grade
- **Given** the monthly report is generated
- **Then** I see attendance broken down by grade:
  | Grade | Students | Avg Attendance | Perfect Attendance |
  |-------|----------|----------------|-------------------|
  | Grade 1 | 80 | 97.5% | 65 |
  | Grade 2 | 75 | 96.8% | 58 |
  | ... | ... | ... | ... |

### AC5: Chronic Absenteeism Report
- **Given** the monthly report is generated
- **Then** I see students with chronic absenteeism (>10% absence):
  - Student name, grade, section
  - Days absent
  - Absence rate
  - Trend (improving/worsening)

### AC6: Monthly Trend Chart
- **Given** the monthly report is generated
- **Then** I see a chart showing:
  - Daily attendance rate over the month
  - Highlight low attendance days

### AC7: Year-to-Date Comparison
- **Given** the monthly report is generated
- **Then** I see YTD statistics:
  - "February: 96.5% vs YTD Average: 95.8%"
  - Month-over-month trend

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Month with holidays | Exclude holiday days from calculations |
| Partial month (start of year) | Calculate based on available days |
| No data for month | Show "No attendance data" |
| Student enrolled mid-month | Pro-rate calculations |
| Transfer students | Show in original and new class |

---

## Test Scenarios

- [ ] Month picker works correctly
- [ ] Report generates successfully
- [ ] School days count accurate (excludes weekends/holidays)
- [ ] Weekly breakdown correct
- [ ] Grade breakdown accurate
- [ ] Chronic absenteeism identified
- [ ] Trend chart displays
- [ ] YTD comparison works
- [ ] Filter by Grade works
- [ ] Large dataset performs well
- [ ] Holiday handling correct

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0046](US0046-weekly-report.md) | Shared logic | Weekly calculations | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
