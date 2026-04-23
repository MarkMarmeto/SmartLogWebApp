# US0023: Create Faculty Record

> **Status:** Done
> **Epic:** [EP0004: Faculty Management](../epics/EP0004-faculty-management.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to create a new faculty record with employee details
**So that** I can maintain an accurate directory of school staff

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who onboards new teachers and staff.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Create Faculty Form
- **Given** I am logged in as Admin Amy
- **When** I navigate to Faculty > Add New Faculty
- **Then** I see a form with fields:
  - Employee ID (required, unique)
  - First Name (required)
  - Last Name (required)
  - Department (required, dropdown)
  - Position/Title (required)
  - Email (optional)
  - Phone Number (optional)
  - Hire Date (optional)

### AC2: Employee ID Validation
- **Given** I am filling out the faculty form
- **When** I enter Employee ID "EMP-2026-001"
- **Then** the system validates it is unique
- **And** shows error if duplicate: "Employee ID already exists"

### AC3: Employee ID Format
- **Given** I am creating a faculty record
- **When** I enter an Employee ID
- **Then** it accepts alphanumeric characters, hyphens, and underscores
- **And** maximum length is 20 characters

### AC4: Save Faculty Record
- **Given** I have filled all required fields for "Juan Dela Cruz"
- **When** I click "Save"
- **Then** the faculty record is created with IsActive = true
- **And** I see success message "Faculty member created successfully"
- **And** I am redirected to the faculty details page

### AC5: Department Dropdown
- **Given** I am on the create faculty form
- **Then** the Department dropdown includes predefined options:
  - Mathematics
  - Science
  - English
  - Filipino
  - Social Studies
  - Physical Education
  - Arts
  - Technology
  - Administration
  - Support Staff

### AC6: Audit Log Entry
- **Given** I create a new faculty record
- **Then** an audit log entry is created with:
  - Action: "FacultyCreated"
  - FacultyId
  - PerformedBy: my user ID

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Duplicate Employee ID | Show error, do not save |
| Missing required fields | Highlight fields, show validation errors |
| Very long name | Truncate at 100 characters |
| Special characters in name | Allow (names like "O'Brien" or "José") |
| Network error on save | Show error, preserve form data |
| Session expired | Redirect to login, preserve form data |
| Invalid email format | Show validation error |
| Phone with spaces/dashes | Accept and normalize |

---

## Test Scenarios

- [ ] Create faculty form displays all fields
- [ ] Required field validation works
- [ ] Employee ID uniqueness validated
- [ ] Employee ID format validated
- [ ] Department dropdown populated
- [ ] Save creates faculty record
- [ ] Success message displayed
- [ ] Redirect to details page works
- [ ] Audit log entry created
- [ ] Cancel returns to faculty list
- [ ] Special characters in name handled
- [ ] Email format validated

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0001](US0001-admin-login.md) | Functional | Admin must be logged in | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Admin role required | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
