# US0036: Attendance Filtering and Search

> **Status:** Done
> **Epic:** [EP0006: Attendance Tracking](../epics/EP0006-attendance-tracking.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to filter and search the attendance dashboard
**So that** I can quickly find specific students or view attendance by grade/section

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who needs to locate specific student attendance quickly.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Filter by Grade
- **Given** I am on the attendance dashboard
- **When** I select "Grade 5" from the Grade filter
- **Then** only Grade 5 students are shown
- **And** summary cards update to show Grade 5 totals

### AC2: Filter by Section
- **Given** I am on the attendance dashboard
- **When** I select "Section A" from the Section filter
- **Then** only Section A students are shown
- **And** summary cards update accordingly

### AC3: Combined Filters
- **Given** I select Grade "5" AND Section "A"
- **Then** only Grade 5, Section A students are shown
- **And** summary shows: "Grade 5 - Section A: 28/30 Present (93%)"

### AC4: Filter by Status
- **Given** I am on the attendance dashboard
- **When** I select "Absent" from the Status filter
- **Then** only absent students are shown
- **And** this helps identify who needs follow-up

### AC5: Search by Name
- **Given** I am on the attendance dashboard
- **When** I enter "Maria" in the search box
- **Then** I see only students whose name contains "Maria"
- **And** their attendance status is shown

### AC6: Search by Student ID
- **Given** I am on the attendance dashboard
- **When** I enter "STU-2026-001" in the search box
- **Then** I see the matching student's attendance

### AC7: Clear Filters
- **Given** I have filters applied
- **When** I click "Clear Filters"
- **Then** all filters reset to default
- **And** I see school-wide attendance again

### AC8: Filter Persistence
- **Given** I apply filters and refresh the page
- **Then** filters are preserved via URL query parameters
- **And** I can bookmark/share filtered views

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No results match filter | Show "No students match filters" |
| Invalid search query | Show no results (don't error) |
| Filter + Search combined | Apply both (AND logic) |
| Special characters in search | Escape safely |
| Very long search query | Truncate at 100 characters |
| Filter non-existent grade | Show empty results |

---

## Test Scenarios

- [ ] Filter by Grade works
- [ ] Filter by Section works
- [ ] Combined Grade + Section filter works
- [ ] Filter by Status (Absent only) works
- [ ] Search by name works (partial match)
- [ ] Search by Student ID works
- [ ] Summary cards update with filters
- [ ] Clear Filters resets all
- [ ] Filters persist on page refresh
- [ ] URL contains filter parameters
- [ ] Empty results handled gracefully
- [ ] Filters work with pagination

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0034](US0034-attendance-dashboard.md) | Functional | Dashboard exists | Draft |
| [US0018](US0018-student-list.md) | Similar pattern | Filter UI patterns | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
