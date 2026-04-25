# US0018: Student List with Search and Filter

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to view, search, and filter the list of students
**So that** I can quickly find specific student records

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who needs quick access to student records during phone calls.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: View Student List
- **Given** I am logged in as Admin Amy
- **When** I navigate to Students
- **Then** I see a table with columns: Student ID, Name, Grade, Section, Parent Phone, Status, Actions
- **And** students are displayed 20 per page
- **And** only active students are shown by default

### AC2: Search by Name
- **Given** I am on the Student List page
- **When** I enter "Maria" in the search box
- **Then** I see only students whose first name or last name contains "Maria"

### AC3: Search by Student ID
- **Given** I am on the Student List page
- **When** I enter "STU-2026" in the search box
- **Then** I see only students whose Student ID contains "STU-2026"

### AC4: Filter by Grade
- **Given** I am on the Student List page
- **When** I select "Grade 5" from the Grade filter
- **Then** I see only students in Grade 5

### AC5: Filter by Section
- **Given** I am on the Student List page
- **When** I select "Section A" from the Section filter
- **Then** I see only students in Section A

### AC6: Teacher View (Read-Only)
- **Given** I am logged in as Teacher Tina
- **When** I navigate to Students
- **Then** I see the student list (read-only)
- **And** I do NOT see Create, Edit, or Deactivate buttons
- **And** I can click to view student details

### AC7: Staff View (Limited)
- **Given** I am logged in as Staff Sarah
- **When** I navigate to Students
- **Then** I can search for students
- **And** I can view basic student details
- **And** I do NOT see parent phone numbers (privacy)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No students in system | Show "No students found" |
| Search with partial ID | Match partial Student IDs |
| Filter Grade + Section | Combined filter (AND logic) |
| Large dataset (10,000 students) | Pagination, fast search |
| Special characters in search | Escape safely |
| Network error | Show error, preserve filters |

---

## Test Scenarios

- [ ] Student list displays with pagination
- [ ] Search by first name works
- [ ] Search by last name works
- [ ] Search by Student ID works
- [ ] Filter by Grade works
- [ ] Filter by Section works
- [ ] Combined filters work
- [ ] Teacher sees read-only view
- [ ] Staff sees limited view (no phone)
- [ ] Inactive students hidden by default
- [ ] Show inactive toggle works
- [ ] Empty results show message

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Functional | Students to list | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Role-based views | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
