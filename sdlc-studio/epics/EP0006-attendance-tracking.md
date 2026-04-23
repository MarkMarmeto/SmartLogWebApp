# EP0006: Attendance Tracking

> **Status:** Done
> **Owner:** TBD
> **Reviewer:** TBD
> **Created:** 2026-02-03
> **Target Release:** Phase 2

## Summary

Provide a real-time attendance dashboard showing student entry/exit status. Enable teachers to view class attendance and administrators to monitor school-wide attendance patterns.

## Inherited Constraints

> See PRD and TRD for full constraint details. Key constraints for this epic:

| Source | Type | Constraint | Impact |
|--------|------|------------|--------|
| PRD | Performance | Dashboard load < 2 seconds | Query optimization |
| PRD | Data | Attendance derived from scan records | No separate attendance table |
| TRD | Architecture | Server-rendered Razor Pages | No real-time WebSocket (polling OK) |

---

## Business Context

### Problem Statement
Teachers need to know which students are present without manual roll call. Administrators need visibility into school-wide attendance for safety (fire drills, emergencies) and reporting purposes.

**PRD Reference:** [Feature FT-009](../prd.md#3-feature-inventory)

### Value Proposition
- Real-time visibility into student presence
- Teachers save time on manual roll call
- Quick identification of absent students
- Emergency readiness (who is on campus?)

### Success Metrics

| Metric | Current | Target | Measurement |
|--------|---------|--------|-------------|
| Time for roll call | Manual: 5 min | < 30 seconds | Teacher feedback |
| Dashboard refresh rate | N/A | < 30 seconds | Auto-refresh interval |
| Attendance data accuracy | N/A | 100% | Scan vs dashboard match |

---

## Scope

### In Scope
- School-wide attendance dashboard (Admin view)
- Class attendance view (Teacher view)
- Student status: Present, Absent, Departed (entry + exit scans)
- Filter by grade, section, date
- Auto-refresh (polling every 30 seconds)
- Today's attendance summary
- Search for specific student's status

### Out of Scope
- Historical attendance reports (EP0008)
- Attendance alerts/notifications
- Manual attendance override
- Attendance for faculty/staff
- Real-time push notifications (WebSocket)

### Affected Personas
- **Admin Amy (Administrator):** School-wide attendance view
- **Teacher Tina:** Class attendance for assigned classes

---

## Acceptance Criteria (Epic Level)

- [ ] Admin can view school-wide attendance dashboard
- [ ] Dashboard shows: Total enrolled, Present, Absent, Departed
- [ ] Dashboard auto-refreshes every 30 seconds
- [ ] Admin can filter by grade and section
- [ ] Admin can search for a specific student
- [ ] Teacher can view attendance for their class(es)
- [ ] Student status shows: Present (entry scan), Departed (exit scan), Absent (no scans)
- [ ] Dashboard loads within 2 seconds
- [ ] Date picker allows viewing historical days
- [ ] Dashboard shows last scan time per student

---

## Dependencies

### Blocked By

| Dependency | Type | Status | Owner |
|------------|------|--------|-------|
| EP0001: Authentication & Authorization | Epic | Not Started | Development |
| EP0003: Student Management | Epic | Not Started | Development |
| EP0005: Scanner Integration | Epic | Not Started | Development |

### Blocking

| Item | Type | Impact |
|------|------|--------|
| EP0007: SMS Notifications | Epic | Triggers based on attendance status |
| EP0008: Reporting | Epic | Attendance data for reports |

---

## Risks & Assumptions

### Assumptions
- Teachers are assigned to specific classes (future: class assignment feature)
- One entry + one exit scan per day is typical
- 30-second refresh is acceptable (no real-time requirement)

### Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Dashboard performance with many students | Medium | Medium | Database indexing, pagination |
| Stale data confusion | Low | Low | Clear "last updated" timestamp |
| Teacher class assignment unclear | Medium | Medium | Initially show all students, filter later |

---

## Technical Considerations

### Architecture Impact
- AttendanceService to aggregate scan data
- Optimized queries for dashboard performance
- Consider caching for frequently accessed data
- Polling mechanism for auto-refresh

### Integration Points
- EP0005: Reads scan records
- EP0003: Links to student records
- Database: Scan table aggregation queries

---

## Sizing

**Story Points:** 14
**Estimated Story Count:** 5

**Complexity Factors:**
- Dashboard UI design
- Query optimization for large datasets
- Auto-refresh mechanism
- Role-based view filtering (Admin vs Teacher)

---

## Story Breakdown

| ID | Title | Points | Status |
|----|-------|--------|--------|
| [US0034](../stories/US0034-attendance-dashboard.md) | School-Wide Attendance Dashboard | 5 | Done |
| [US0035](../stories/US0035-class-attendance.md) | Class Attendance View | 3 | Done |
| [US0036](../stories/US0036-attendance-filter-search.md) | Attendance Filtering and Search | 2 | Done |
| [US0037](../stories/US0037-dashboard-auto-refresh.md) | Dashboard Auto-Refresh | 2 | Done |
| [US0038](../stories/US0038-historical-attendance.md) | Historical Date Selection | 2 | Done |

**Total:** 14 story points across 5 stories

---

## Test Plan

**Test Spec:** Will be generated with `/sdlc-studio test-spec --epic EP0006`

---

## Open Questions

- [ ] How are teachers assigned to classes? (Manual or automatic?) - Owner: Product
- [ ] Should "Departed" status require both entry AND exit scan? - Owner: Product
- [ ] What time defines the "school day" for attendance? - Owner: Product

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial epic created |
