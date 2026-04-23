# US0011: Deactivate/Reactivate User

> **Status:** Done
> **Epic:** [EP0002: User Management](../epics/EP0002-user-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to deactivate users who leave the school and reactivate them if they return
**So that** former staff cannot access the system but records are preserved

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages staff lifecycle.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Deactivate User
- **Given** I am viewing user "teacher.john" who is active
- **When** I click "Deactivate"
- **Then** I see a confirmation dialog "Are you sure you want to deactivate this user? They will no longer be able to log in."
- **When** I confirm
- **Then** the user's IsActive flag is set to false
- **And** I see success message "User deactivated"
- **And** an audit log entry is created

### AC2: Deactivated User Cannot Login
- **Given** user "teacher.john" has been deactivated
- **When** they attempt to log in with correct credentials
- **Then** login is denied with message "Your account has been deactivated"

### AC3: Reactivate User
- **Given** I am viewing deactivated user "teacher.john"
- **When** I click "Reactivate"
- **Then** the user's IsActive flag is set to true
- **And** I see success message "User reactivated"
- **And** an audit log entry is created

### AC4: Cannot Deactivate Self
- **Given** I am logged in as Admin Amy
- **When** I view my own user record
- **Then** the "Deactivate" button is not visible or disabled
- **And** I see note "You cannot deactivate your own account"

### AC5: Cannot Deactivate Higher Role
- **Given** I am logged in as Admin (not Super Admin)
- **When** I view a Super Admin user
- **Then** the "Deactivate" button is not visible

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Deactivate user with active session | Session invalidated, user logged out |
| Reactivate user with locked account | Unlock and reactivate both |
| Deactivate last Super Admin | Prevent with error "Cannot deactivate the last Super Admin" |
| Cancel deactivation dialog | No changes made |
| Network error during deactivation | Show error, no changes made |

---

## Test Scenarios

- [ ] Deactivate active user sets IsActive to false
- [ ] Deactivated user cannot log in
- [ ] Reactivate user sets IsActive to true
- [ ] Reactivated user can log in
- [ ] Cannot deactivate own account
- [ ] Admin cannot deactivate Super Admin
- [ ] Confirmation dialog shown before deactivation
- [ ] Audit log entry created on deactivate
- [ ] Audit log entry created on reactivate
- [ ] Last Super Admin cannot be deactivated

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0009](US0009-create-user.md) | Functional | Users to deactivate | Draft |
| [US0001](US0001-user-login.md) | Functional | Login checks IsActive | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
