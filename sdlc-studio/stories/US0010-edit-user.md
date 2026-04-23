# US0010: Edit User Details

> **Status:** Done
> **Epic:** [EP0002: User Management](../epics/EP0002-user-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to edit existing user account details
**So that** I can update staff information when it changes

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who maintains staff records.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Access Edit User Form
- **Given** I am logged in as Admin Amy
- **When** I click "Edit" on a user in the user list
- **Then** I see a form pre-populated with the user's current details

### AC2: Successful Edit
- **Given** I am on the Edit User form for "teacher.john"
- **When** I change the last name from "Smith" to "Johnson"
- **And** I click "Save Changes"
- **Then** the user's last name is updated
- **And** I see success message "User updated successfully"
- **And** an audit log entry is created

### AC3: Cannot Edit Own Role
- **Given** I am logged in as Admin Amy
- **When** I view my own user record for editing
- **Then** the Role field is disabled/read-only
- **And** I see a note "You cannot change your own role"

### AC4: Role Change Restrictions
- **Given** I am logged in as Admin (not Super Admin)
- **And** I am editing another Admin user
- **Then** I cannot change their role to Super Admin
- **And** I cannot demote them (Admin can only manage lower roles)

### AC5: Username Cannot Be Changed
- **Given** I am on the Edit User form
- **Then** the Username field is read-only
- **And** I see a note "Username cannot be changed"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Edit user who was just deactivated | Show "User not found or deactivated" |
| Change email to existing email | Show "Email already in use" |
| Concurrent edit by two admins | Last save wins, show notification |
| Edit Super Admin as Admin | Access denied (403) |
| Clear required field | Validation error on save |
| Network error during save | Show error, preserve form data |

---

## Test Scenarios

- [ ] Edit user name fields successfully
- [ ] Edit user email successfully
- [ ] Edit user phone successfully
- [ ] Edit user role successfully (within permission)
- [ ] Cannot edit own role
- [ ] Cannot change username
- [ ] Admin cannot edit Super Admin users
- [ ] Audit log entry created on edit
- [ ] Validation errors for invalid data
- [ ] Email uniqueness enforced

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0009](US0009-create-user.md) | Functional | Users to edit | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
