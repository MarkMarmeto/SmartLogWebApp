# US0038: Historical Date Selection

> **Status:** Done
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to view attendance for past dates
**So that** I can review historical attendance patterns and investigate specific days

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who reviews attendance records.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Date Picker
- **Given** I am on the attendance dashboard
- **Then** I see a date picker showing today's date
- **And** I can select a different date

### AC2: View Past Attendance
- **Given** I select February 3, 2026 from the date picker
- **Then** the dashboard shows attendance for that date
- **And** the header updates: "Attendance for February 3, 2026"
- **And** summary cards show that day's totals

### AC3: Date Navigation Arrows
- **Given** I am viewing attendance for February 3, 2026
- **When** I click the "Previous Day" arrow
- **Then** I see attendance for February 2, 2026
- **And** clicking "Next Day" returns to February 3

### AC4: Cannot Select Future Dates
- **Given** today is February 4, 2026
- **When** I try to select February 5, 2026
- **Then** future dates are disabled in the picker
- **And** I cannot view future attendance

### AC5: Quick Date Options
- **Given** I am on the attendance dashboard
- **Then** I see quick links:
  - "Today"
  - "Yesterday"
- **And** clicking them jumps to that date

### AC6: Weekday Indicator
- **Given** I select a date
- **Then** I see the day of week: "Monday, February 3, 2026"
- **And** weekends are visually indicated (may have low/no attendance)

### AC7: Historical Data Accuracy
- **Given** I view attendance for a past date
- **Then** the data reflects the attendance as it was recorded
- **And** changes to student status (deactivated since) don't affect historical view

### AC8: URL Date Parameter
- **Given** I select a date
- **Then** the URL includes the date: `/attendance?date=2026-02-03`
- **And** I can bookmark or share the specific date view

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Select date before system launch | Show "No data for this date" |
| Select date with no scans | Show 0 present, all absent |
| Date before student enrolled | Student not in historical view |
| Date after student deactivated | Student appears in historical view |
| Invalid date in URL | Default to today |
| Very old date (years ago) | Allow access, show data if exists |

---

## Test Scenarios

- [ ] Date picker displays and works
- [ ] Past date shows historical attendance
- [ ] Header updates with selected date
- [ ] Previous/Next day arrows work
- [ ] Future dates disabled
- [ ] "Today" quick link works
- [ ] "Yesterday" quick link works
- [ ] Day of week displayed
- [ ] Historical data is accurate
- [ ] URL includes date parameter
- [ ] Invalid date defaults to today
- [ ] Works with filters (Grade + Date)

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0034](US0034-attendance-dashboard.md) | Functional | Dashboard exists | Draft |
| [US0036](US0036-attendance-filter-search.md) | Integration | Works with filters | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
