# US0013: User List with Search and Filter

> **Status:** Done
> **Epic:** [EP0002: User Management](../epics/EP0002-user-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to view, search, and filter the list of users
**So that** I can quickly find specific staff members

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who needs to quickly find staff records.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: View User List
- **Given** I am logged in as Admin Amy
- **When** I navigate to Users
- **Then** I see a table with columns: Username, Name, Email, Role, Status, Actions
- **And** users are displayed 20 per page
- **And** pagination controls are visible

### AC2: Search by Name
- **Given** I am on the User List page
- **When** I enter "John" in the search box
- **And** I click Search (or press Enter)
- **Then** I see only users whose first name or last name contains "John"

### AC3: Search by Username
- **Given** I am on the User List page
- **When** I enter "teacher" in the search box
- **Then** I see only users whose username contains "teacher"

### AC4: Filter by Role
- **Given** I am on the User List page
- **When** I select "Teacher" from the Role filter dropdown
- **Then** I see only users with the Teacher role

### AC5: Filter by Status
- **Given** I am on the User List page
- **When** I select "Inactive" from the Status filter dropdown
- **Then** I see only deactivated users

### AC6: Combined Search and Filter
- **Given** I have searched for "John"
- **When** I also filter by Role "Teacher"
- **Then** I see only Teachers whose name contains "John"

### AC7: Empty Results
- **Given** I search for "xyz123nonexistent"
- **Then** I see "No users found matching your criteria"
- **And** I see a "Clear filters" link

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No users in system | Show "No users found" message |
| Search with special characters | Escape for SQL safety |
| Very long search term | Truncate or limit input |
| Page beyond available results | Show last valid page |
| Rapid filter changes | Debounce requests |
| Network error during search | Show error, preserve filters |
| Case-insensitive search | "john" matches "John" |
| Search clears on page navigation | Preserve filters in URL params |

---

## Test Scenarios

- [ ] User list displays with pagination
- [ ] 20 users per page
- [ ] Search by first name works
- [ ] Search by last name works
- [ ] Search by username works
- [ ] Filter by role works
- [ ] Filter by status works
- [ ] Combined search and filter works
- [ ] Empty results show message
- [ ] Clear filters resets view
- [ ] Pagination works correctly
- [ ] Search is case-insensitive

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0009](US0009-create-user.md) | Functional | Users to list | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
