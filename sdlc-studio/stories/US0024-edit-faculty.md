# US0024: Edit Faculty Details

> **Status:** Done
> **Epic:** [EP0004: Faculty Management](../epics/EP0004-faculty-management.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to edit an existing faculty member's information
**So that** I can keep their records up to date when details change

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who maintains accurate faculty records.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Edit Faculty Button
- **Given** I am on the faculty details page for "Juan Dela Cruz"
- **Then** I see an "Edit" button

### AC2: Edit Form Pre-populated
- **Given** I click "Edit" on faculty "Juan Dela Cruz"
- **Then** the edit form opens with all current data pre-filled:
  - Employee ID: "EMP-2026-001"
  - First Name: "Juan"
  - Last Name: "Dela Cruz"
  - Department: "Mathematics"
  - Position: "Teacher"
  - Email, Phone, Hire Date (if set)

### AC3: Employee ID Cannot Be Changed
- **Given** I am editing faculty details
- **Then** the Employee ID field is read-only
- **And** I see a note: "Employee ID cannot be changed"

### AC4: Save Changes
- **Given** I change the department from "Mathematics" to "Science"
- **When** I click "Save"
- **Then** the faculty record is updated
- **And** I see success message "Faculty details updated successfully"
- **And** I am redirected to the faculty details page

### AC5: Edit Deactivated Faculty
- **Given** a faculty member is deactivated
- **When** I view their details
- **Then** the "Edit" button is available
- **And** I can update their information
- **And** the IsActive status is NOT changed by editing

### AC6: Audit Log Entry
- **Given** I edit a faculty record
- **Then** an audit log entry is created with:
  - Action: "FacultyUpdated"
  - FacultyId
  - PerformedBy: my user ID
  - Details: changed fields (old → new)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No changes made | Allow save (idempotent) |
| Clear optional field | Save as null/empty |
| Concurrent edit | Last save wins, show warning |
| Network error on save | Show error, preserve changes |
| Cancel with unsaved changes | Confirm "Discard changes?" |
| Session expired | Redirect to login |
| Invalid email format | Show validation error |

---

## Test Scenarios

- [ ] Edit button visible on faculty details
- [ ] Form pre-populated with current data
- [ ] Employee ID is read-only
- [ ] Required fields validated
- [ ] Changes saved successfully
- [ ] Success message displayed
- [ ] Redirect to details page works
- [ ] Audit log entry created with change details
- [ ] Cancel prompts for confirmation
- [ ] Deactivated faculty can be edited
- [ ] Clear optional fields works

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0023](US0023-create-faculty.md) | Functional | Faculty exists | Draft |
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
