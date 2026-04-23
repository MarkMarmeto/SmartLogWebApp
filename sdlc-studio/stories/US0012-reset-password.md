# US0012: Reset User Password

> **Status:** Done
> **Epic:** [EP0002: User Management](../epics/EP0002-user-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to reset a user's password when they forget it
**So that** they can regain access to their account

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who helps staff with account issues.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Reset Password Action
- **Given** I am viewing user "teacher.john"
- **When** I click "Reset Password"
- **Then** I see a confirmation dialog "This will generate a new temporary password. Continue?"

### AC2: Successful Password Reset
- **Given** I confirm the password reset
- **Then** a new temporary password is generated
- **And** the user's password is updated
- **And** I see the temporary password displayed
- **And** I see message "Password reset. New temporary password: [password]"
- **And** an audit log entry is created

### AC3: Force Password Change
- **Given** user "teacher.john" had their password reset
- **When** they log in with the temporary password
- **Then** they are redirected to a "Change Password" page
- **And** they must set a new password before continuing

### AC4: Cannot Reset Own Password (Use Profile)
- **Given** I am logged in as Admin Amy
- **When** I view my own user record
- **Then** "Reset Password" is replaced with "Change Password" link
- **And** I am directed to my profile page to change my password

### AC5: Cannot Reset Higher Role Password
- **Given** I am logged in as Admin (not Super Admin)
- **When** I view a Super Admin user
- **Then** the "Reset Password" button is not visible

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Reset password for deactivated user | Allow (user may be reactivated later) |
| Reset password for locked user | Allow and unlock the account |
| Temporary password expires | After first login (must change immediately) |
| Admin copies password incorrectly | User must request another reset |
| User has 2FA enabled | 2FA remains enabled, only password changes |
| Network error during reset | Show error, password unchanged |

---

## Test Scenarios

- [ ] Password reset generates new temporary password
- [ ] Temporary password allows login
- [ ] User forced to change password after using temporary
- [ ] Audit log entry created on password reset
- [ ] Cannot reset own password via this function
- [ ] Admin cannot reset Super Admin password
- [ ] Password reset unlocks locked account
- [ ] 2FA not affected by password reset
- [ ] Confirmation dialog shown before reset
- [ ] Cancel dialog preserves existing password

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0009](US0009-create-user.md) | Functional | Users exist | Draft |
| [US0001](US0001-user-login.md) | Functional | Login flow | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
