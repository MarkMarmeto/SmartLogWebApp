# US0108: Attendance — Non-Graded Filter Handling

> **Status:** Done
> **Epic:** [EP0010: Programs & Sections Overhaul](../epics/EP0010-programs-sections-overhaul.md)
> **Plan:** [PL0030: Attendance — Non-Graded Filter Handling](../plans/PL0030-attendance-ng-filter-handling.md)
> **Owner:** TBD
> **Created:** 2026-04-26
> **Marked Ready:** 2026-04-26
> **Marked Planned:** 2026-04-26
> **Scope amended:** 2026-04-26 — see Revision History

## User Story

**As a** Admin Amy (Administrator) / Teacher Tina
**I want** the Attendance API to handle Non-Graded students cleanly — excluded by default when filtering by Program, and reachable via a dedicated grade-level filter
**So that** Program-scoped queries never accidentally include NG students, and I can still produce NG-only listings when needed.

## Context

### Persona Reference
**Admin Amy** — runs daily/weekly/monthly attendance reports. **Teacher Tina** — checks her class roster.

### Background
With NG students having no Program, any `?program=` filter must exclude them (decision 2026-04-26: exclude). A `?gradeLevel=NG` filter must work to scope queries to NG only. The natural SQL semantics of `WHERE Program = 'STEM'` already exclude `NULL`, so no special-case code is needed in the service layer — but we lock the contract in with explicit tests.

Originally the story also covered Reports + Dashboard NG handling (Program column rendering, Program filter dropdown gating, Dashboard `?program=` filter). Inspection 2026-04-26 found that **Reports have no Program column or filter today** and **Dashboard's `attendance-by-grade` endpoint has no `?program=` parameter**. Adding those features is out of scope for the NG epic — descoped to a future story (potential US0110). This story now focuses narrowly on the AttendanceApi paths.

---

## Inherited Constraints

| Source | Type | Constraint | AC Implication |
|--------|------|------------|----------------|
| US0103 | Data | NG sections have no Program | `?program=` filters cannot include NG |
| Stakeholder | Decision | Exclude NG from Program-filtered reports (2026-04-26) | Default behaviour confirmed |
| EP0008 | Reports | CSV/HTML exports already support filters | NG handling propagates to exports |

---

## Acceptance Criteria

### AC1: `?program=` Filter Excludes NG
- **Given** I call `GET /api/v1/attendance/list?program=REGULAR` (or any Program code)
- **Then** the response contains zero NG students
- **And** graded students with that Program are returned as before

### AC2: `?program=` with No Value (default) Includes NG
- **Given** I call attendance APIs without a `program` filter
- **Then** NG students are included in results alongside graded students

### AC3: `?grade=NG` Filter Works
- **Given** I call `GET /api/v1/attendance/list?grade=NG`
- **Then** the response includes only NG students (across all four LEVEL sections)
- **And** the existing `grade` filter parameter is reused — no new parameter introduced

### AC4: Combined `?program=...&grade=NG` Returns Empty
- **Given** I call `?program=STEM&grade=NG`
- **Then** the response is empty (NG students have null Program, so `Program == "STEM"` excludes them)
- **And** no special-case warning header is needed — the empty result is the correct contract

### AC5: Audit-Logs Export Unchanged
- **Given** the audit-logs export already does not filter by Program
- **Then** no change is required there

---

## Scope

### In Scope
- `AttendanceApiController` — verify `program` filter excludes NG; verify `grade=NG` works
- `AttendanceService` — verify SQL filter semantics; lock contract in tests
- Tests covering AC1-AC4

### Out of Scope (Descoped 2026-04-26 — see Background)
- `ReportsApiController` Program filter — Reports have no Program column or filter today
- `DashboardApiController` Program filter — `attendance-by-grade` has no `?program=` parameter today
- Razor report pages — Reports have no Program filter UI today
- Adding a Program column to reports — pre-existing gap, out of EP0010 scope
- Teacher dashboards
- Group-by-program report aggregations
- Bulk import of NG students (verified in US0103/US0106)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| `?program=REGULAR` requested | NG still excluded — REGULAR is a Program, NG has none |
| `?program=STEM&grade=NG` | Returns empty: AND of two filters excludes NG (Program=null) and excludes non-NG students. Correct contract. |
| Empty result on combined filter | Caller's responsibility — frontend should not combine these orthogonal filters without intent |

---

## Test Scenarios

- [ ] `program=REGULAR` returns 0 NG rows
- [ ] No filter returns NG and graded rows together
- [ ] `grade=NG` returns only NG rows
- [ ] `program=REGULAR&grade=NG` returns empty (correct AND-of-filters contract)

---

## Technical Notes

### Files to Verify (Likely No Code Change)
- `src/SmartLog.Web/Services/AttendanceService.cs` — already filters via `s.Program == programFilter` (line 50) and `s.GradeLevel == gradeFilter` (line 40). NG behaves correctly today.
- `src/SmartLog.Web/Controllers/Api/AttendanceApiController.cs` — already accepts `?program=` and `?grade=` query parameters.

### Filter Logic (Already Correct)
```csharp
if (!string.IsNullOrWhiteSpace(programFilter))
    studentsQuery = studentsQuery.Where(s => s.Program == programFilter); // excludes NG (Program is null)

if (!string.IsNullOrWhiteSpace(gradeFilter))
    studentsQuery = studentsQuery.Where(s => s.GradeLevel == gradeFilter); // matches "NG" for NG students
```

The SQL semantics `WHERE Program = 'X'` exclude NULL — no special-case code required.

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0106](US0106-student-program-null-for-ng.md) | Data | Student.Program null for NG | Draft |
| [US0105](US0105-seed-ng-gradelevel-and-sections.md) | Data | NG sections seeded | Draft |

---

## Estimation

**Story Points:** 1
**Complexity:** Trivial — likely no production code change. Tests lock the contract in.

## Follow-Up

A separate future story (provisional **US0110: "Add Program column + filter to Reports & Dashboard"**) should cover:
- Adding a Program column to daily/weekly/monthly attendance reports
- Adding a Program filter dropdown to report Razor pages
- Adding `?program=` to Dashboard `attendance-by-grade` endpoint
- NG-aware rendering ("—" or empty in Program column for NG rows)

This work is **not in EP0010** scope and was descoped from US0108 on 2026-04-26 after inspection revealed Reports have no Program column today.

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-26 | Claude | Initial draft from NG-no-program planning session |
| 2026-04-26 | Claude | Scope trimmed: AC4-AC6 (Reports Program column, Reports Program filter UI, Dashboard `?program=`) descoped after inspection found Reports have no Program column/filter today and Dashboard `attendance-by-grade` has no `?program=` parameter. Story now focuses narrowly on AttendanceApi NG behaviour. Future US0110 will cover the descoped infrastructure work. Story points 5 → 1. Title shortened. |
