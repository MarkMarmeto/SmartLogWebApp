# US0027: Link/Unlink Faculty to User Account

> **Status:** Done
> **Epic:** [EP0004: Faculty Management](../epics/EP0004-faculty-management.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to link a faculty record to a user account or unlink them
**So that** faculty members can have system access while maintaining separate personnel records

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages faculty system access.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Link Faculty Action
- **Given** I am on the faculty details page for "Juan Dela Cruz" (not linked to any user)
- **Then** I see a "Link to User Account" button

### AC2: User Selection
- **Given** I click "Link to User Account"
- **Then** I see a searchable list of user accounts
- **And** only users NOT already linked to a faculty record are shown
- **And** I can search by username or email

### AC3: Link Confirmation
- **Given** I select user "jdelacruz@school.edu" to link to faculty "Juan Dela Cruz"
- **Then** I see confirmation: "Link Juan Dela Cruz to user account jdelacruz@school.edu?"
- **And** I can confirm or cancel

### AC4: Link Effect
- **Given** I confirm the link
- **Then** the faculty record's UserId field is set to the selected user
- **And** I see success message "Faculty linked to user account successfully"
- **And** the faculty details page shows "Linked User: jdelacruz@school.edu"

### AC5: Unlink Faculty Action
- **Given** I am on the faculty details page for "Juan Dela Cruz" (linked to a user)
- **Then** I see an "Unlink User Account" button

### AC6: Unlink Confirmation
- **Given** I click "Unlink User Account"
- **Then** I see confirmation: "Remove link between Juan Dela Cruz and user account jdelacruz@school.edu? The user account will NOT be deleted."
- **And** I can confirm or cancel

### AC7: Unlink Effect
- **Given** I confirm the unlink
- **Then** the faculty record's UserId field is set to null
- **And** I see success message "Faculty unlinked from user account"
- **And** the faculty details page shows "No linked user account"

### AC8: Audit Log Entries
- **Given** I link or unlink a faculty record
- **Then** an audit log entry is created with:
  - Action: "FacultyLinkedToUser" or "FacultyUnlinkedFromUser"
  - FacultyId
  - UserId (linked or unlinked)
  - PerformedBy: my user ID

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| User already linked to another faculty | Show error "User already linked to [other faculty name]" |
| Link to deactivated user | Allow with warning |
| Link deactivated faculty to user | Allow (might be temporary deactivation) |
| User deleted after linking | Show "Linked user no longer exists" |
| Network error | Show error, no changes made |
| Cancel confirmation | No changes made |
| No unlinked users available | Show message "No available user accounts" |

---

## Test Scenarios

- [ ] Link button visible for unlinked faculty
- [ ] User search shows only unlinked users
- [ ] Search by username works
- [ ] Search by email works
- [ ] Link confirmation dialog shown
- [ ] Link sets UserId on faculty record
- [ ] Success message displayed
- [ ] Faculty details show linked user
- [ ] Unlink button visible for linked faculty
- [ ] Unlink confirmation dialog shown
- [ ] Unlink clears UserId
- [ ] Audit log entries created
- [ ] Cannot link user already linked to another faculty
- [ ] Cancel preserves current state

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0023](US0023-create-faculty.md) | Functional | Faculty exists | Draft |
| [US0009](US0009-create-user.md) | Functional | User accounts exist | Draft |
| [US0007](US0007-authorization-enforcement.md) | Functional | Admin role required | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
