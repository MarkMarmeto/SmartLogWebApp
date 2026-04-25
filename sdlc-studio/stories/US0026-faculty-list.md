# US0026: Faculty List with Search and Filter

> **Status:** Done
> **Epic:** [EP0004: Faculty Management](../epics/EP0004-faculty-management.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to view, search, and filter the list of faculty members
**So that** I can quickly find specific faculty records

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who needs quick access to faculty information.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: View Faculty List
- **Given** I am logged in as Admin Amy
- **When** I navigate to Faculty
- **Then** I see a table with columns: Employee ID, Name, Department, Position, Email, Status, Actions
- **And** faculty are displayed 20 per page
- **And** only active faculty are shown by default

### AC2: Search by Name
- **Given** I am on the Faculty List page
- **When** I enter "Juan" in the search box
- **Then** I see only faculty whose first name or last name contains "Juan"

### AC3: Search by Employee ID
- **Given** I am on the Faculty List page
- **When** I enter "EMP-2026" in the search box
- **Then** I see only faculty whose Employee ID contains "EMP-2026"

### AC4: Filter by Department
- **Given** I am on the Faculty List page
- **When** I select "Mathematics" from the Department filter
- **Then** I see only faculty in the Mathematics department

### AC5: Filter by Status
- **Given** I am on the Faculty List page
- **When** I select "Show All" or "Inactive Only" from the Status filter
- **Then** I see faculty matching the selected status

### AC6: Combined Filters
- **Given** I have Department filter set to "Science"
- **When** I enter "Maria" in the search box
- **Then** I see only faculty named "Maria" who are in the Science department

### AC7: Teacher View (Read-Only)
- **Given** I am logged in as Teacher Tina
- **When** I navigate to Faculty
- **Then** I see the faculty list (read-only)
- **And** I do NOT see Create, Edit, or Deactivate buttons
- **And** I can click to view faculty details

### AC8: Pagination
- **Given** there are 50 faculty members
- **When** I view the faculty list
- **Then** I see 20 faculty per page
- **And** I can navigate between pages
- **And** I see "Page 1 of 3"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No faculty in system | Show "No faculty found" |
| Search with partial ID | Match partial Employee IDs |
| Filter Department + Search | Combined filter (AND logic) |
| Large dataset (500+ faculty) | Pagination, fast search |
| Special characters in search | Escape safely |
| Network error | Show error, preserve filters |
| Clear all filters | Reset to default view |

---

## Test Scenarios

- [ ] Faculty list displays with pagination
- [ ] Search by first name works
- [ ] Search by last name works
- [ ] Search by Employee ID works
- [ ] Filter by Department works
- [ ] Filter by Status works
- [ ] Combined filters work
- [ ] Teacher sees read-only view
- [ ] Inactive faculty hidden by default
- [ ] Show inactive toggle works
- [ ] Empty results show message
- [ ] Pagination navigation works
- [ ] Sorting by columns works

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0023](US0023-create-faculty.md) | Functional | Faculty to list | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Role-based views | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
