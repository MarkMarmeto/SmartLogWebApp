# US0075: Visitor Scan Log

> **Status:** Done
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to view a log of all visitor scans with entry/exit times and duration
**So that** I can review visitor traffic patterns and maintain security records

## Context

### Background
A dedicated log page shows visitor scan history. Each visit (entry + exit pair) shows the pass code, timestamps, and calculated duration. Filterable by date range.

---

## Acceptance Criteria

### AC1: Visitor Log Page
- **Given** I navigate to `/Admin/VisitorPasses/Log`
- **Then** I see a table of visitor scans:
  - Pass Code | Entry Time | Exit Time | Duration | Device | Status

### AC2: Duration Calculation
- **Given** VISITOR-005 scanned ENTRY at 9:15 AM and EXIT at 10:30 AM
- **Then** the log shows Duration: "1h 15m"

### AC3: Incomplete Visit
- **Given** VISITOR-005 scanned ENTRY at 9:15 AM but no EXIT yet
- **Then** the log shows Exit Time: "—" and Duration: "In progress"

### AC4: Date Range Filter
- **Given** I select date range April 1-15, 2026
- **When** the page filters
- **Then** only visitor scans within that range are shown

### AC5: Pass Code Filter
- **Given** I type "VISITOR-005" in the search box
- **Then** only scans for pass VISITOR-005 are shown

### AC6: Summary Statistics
- **Given** the log page loads for today
- **Then** I see a summary bar:
  - "Today: 12 visitors | Avg duration: 45m | Currently in: 3"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Multiple entry scans without exit | Each entry starts new "visit" row |
| Exit without prior entry | Show as standalone EXIT row |
| No visitor scans for date range | "No visitor activity for selected period" |
| Very long visit (>24 hours) | Show full duration; highlight row with warning "Pass may not have been returned" |
| Pagination with large dataset | Default 50 rows per page; date range filter recommended |

---

## Test Scenarios

- [ ] Log page displays visitor scans
- [ ] Duration calculated from entry/exit pair
- [ ] Incomplete visit shows "In progress"
- [ ] Date range filter works
- [ ] Pass code search works
- [ ] Summary statistics accurate
- [ ] Handles entry-only records

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0073](US0073-visitor-scan-processing.md) | Data | VisitorScan records exist | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
