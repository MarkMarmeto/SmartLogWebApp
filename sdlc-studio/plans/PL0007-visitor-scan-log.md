# PL0007: Visitor Scan Log — Implementation Plan

> **Status:** Done
> **Story:** [US0075: Visitor Scan Log](../stories/US0075-visitor-scan-log.md)
> **Epic:** [EP0012: Visitor Pass System](../epics/EP0012-visitor-pass-system.md)
> **Created:** 2026-04-18
> **Language:** C# / ASP.NET Core 8 Razor Pages

## Overview

Build a visitor scan log page at `/Admin/VisitorPasses/Log` that pairs ENTRY/EXIT scans into "visits", calculates duration, and provides date range and pass code filters. Includes a summary bar with today's visitor count, average duration, and currently-in count. Follows the same admin page patterns as the existing attendance and SMS history pages.

## Acceptance Criteria Summary

| AC | Name | Description |
|----|------|-------------|
| AC1 | Visitor Log Page | Table at `/Admin/VisitorPasses/Log`: Pass Code, Entry Time, Exit Time, Duration, Device, Status |
| AC2 | Duration Calculation | Computed from paired ENTRY→EXIT scans; e.g., "1h 15m" |
| AC3 | Incomplete Visit | No EXIT yet → Exit Time: "—", Duration: "In progress" |
| AC4 | Date Range Filter | Filter scans by start/end date |
| AC5 | Pass Code Filter | Search box filters by pass code (e.g., "VISITOR-005") |
| AC6 | Summary Statistics | "Today: N visitors | Avg duration: Nm | Currently in: N" |

---

## Technical Context

### Language & Framework
- **Primary Language:** C# 12
- **Framework:** ASP.NET Core 8.0 Razor Pages + EF Core 8.0
- **Test Framework:** xUnit (service-level tests for visit pairing logic)

### Existing Patterns
- **Date range filters:** Attendance pages use `[BindProperty(SupportsGet = true)] DateTime? StartDate, EndDate` with `<input type="date">`
- **Search/filter:** `SearchTerm` bound via query string, applied as `.Where()` in LINQ
- **Pagination:** 50 rows per page default for log-type pages
- **Summary bars:** Bootstrap cards at top of page with key metrics

---

## Recommended Approach

**Strategy:** Test-After
**Rationale:** The visit pairing logic (matching ENTRY to nearest EXIT for same pass) is the only complex piece. Implement as a method in `VisitorPassService`, test it there. The Razor page is display-only.

---

## Implementation Phases

### Phase 1: Visit Pairing Logic in VisitorPassService
**Goal:** Query VisitorScans and pair ENTRY→EXIT into visit records

- [ ] Add to `IVisitorPassService`:
  - `Task<VisitorLogResult> GetVisitorLogAsync(DateTime? startDate, DateTime? endDate, string? passCodeFilter, int page, int pageSize)`
- [ ] Add view models:
  - `VisitorVisit`: PassCode, PassNumber, EntryTime (DateTime), ExitTime (DateTime?), Duration (string), DeviceName, Status
  - `VisitorLogResult`: `List<VisitorVisit> Visits`, `int TotalCount`, `VisitorLogSummary Summary`
  - `VisitorLogSummary`: TotalVisitors (int), AvgDurationMinutes (double?), CurrentlyIn (int)
- [ ] Implement pairing logic in `VisitorPassService.GetVisitorLogAsync()`:
  - Query ENTRY scans within date range, ordered by ScannedAt desc
  - For each ENTRY scan, find the nearest EXIT scan for the same VisitorPassId where `ExitScan.ScannedAt > EntryScan.ScannedAt`
  - Calculate duration: `exitTime - entryTime` → format as "Nh Nm" or "In progress" if no exit
  - Summary: count distinct passes with ENTRY today, avg duration of completed visits, count passes currently InUse

### Phase 2: Visitor Log Razor Page
**Goal:** Display paired visits with filters

- [ ] Create `src/SmartLog.Web/Pages/Admin/VisitorPasses/Log.cshtml.cs`
  - `[Authorize(Policy = "RequireAdmin")]`
  - Inject: `IVisitorPassService`
  - Properties:
    - `[BindProperty(SupportsGet = true)] DateTime? StartDate`
    - `[BindProperty(SupportsGet = true)] DateTime? EndDate`
    - `[BindProperty(SupportsGet = true)] string? SearchTerm`
    - `[BindProperty(SupportsGet = true)] int PageNumber = 1`
    - `int PageSize = 50`, `int TotalPages`, `int TotalVisits`
    - `List<VisitorVisit> Visits`
    - `VisitorLogSummary Summary`
  - `OnGetAsync()`: default StartDate/EndDate to today if not set; call `GetVisitorLogAsync()`
- [ ] Create `src/SmartLog.Web/Pages/Admin/VisitorPasses/Log.cshtml`
  - Summary cards row: "Today: N visitors", "Avg duration: Nm", "Currently in: N"
  - Filter bar: date range pickers + pass code search + Apply button
  - Table: Pass Code | Entry Time | Exit Time | Duration | Device | Status
  - Duration column: green text for completed, orange "In progress" for open visits
  - Pagination footer
  - Empty state: "No visitor activity for selected period"

### Phase 3: Navigation Link
**Goal:** Link from VisitorPasses Index to Log page

- [ ] Add "View Scan Log" button/link on `/Admin/VisitorPasses` index page
- [ ] Add breadcrumb: Admin > Visitor Passes > Scan Log

---

## Edge Case Handling

| # | Edge Case (from Story) | Handling Strategy | Phase |
|---|------------------------|-------------------|-------|
| 1 | Multiple entry scans without exit | Each ENTRY starts a new visit row; previous visit shows "—" for exit | Phase 1 |
| 2 | Exit without prior entry | Show as standalone EXIT row with Entry Time: "—", Duration: "—" | Phase 1 |
| 3 | No visitor scans for date range | Display "No visitor activity for selected period" message | Phase 2 |
| 4 | Very long visit (>24 hours) | Show full duration (e.g., "25h 30m"); add CSS class `visit-warning` with orange highlight | Phase 2 |
| 5 | Pagination with large dataset | Default 50 per page; date range filter strongly recommended in UI hint text | Phase 2 |

**Coverage:** 5/5 edge cases handled

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Visit pairing query perf on large datasets | Medium | Index on `(VisitorPassId, ScannedAt)` covers the join; date range filter limits scope |
| Ambiguous ENTRY/EXIT pairing | Low | Simple nearest-next-EXIT algorithm; edge cases documented in UI |

---

## Definition of Done

- [ ] All acceptance criteria implemented
- [ ] Visit pairing logic tested
- [ ] Date range and pass code filters work
- [ ] Summary statistics accurate
- [ ] Edge cases handled
- [ ] Build succeeds (0 errors)
