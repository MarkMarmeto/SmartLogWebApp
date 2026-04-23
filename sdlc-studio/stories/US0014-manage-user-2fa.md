# US0014: Manage User 2FA

> **Status:** Done
> **Epic:** [EP0002: User Management](../epics/EP0002-user-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to enable or disable 2FA for other users
**So that** I can help users who lose access to their authenticator app

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who helps staff with account issues.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: View 2FA Status
- **Given** I am viewing user "teacher.john"
- **Then** I see their 2FA status: "Enabled" or "Disabled"

### AC2: Disable User 2FA
- **Given** user "teacher.john" has 2FA enabled
- **When** I click "Disable 2FA"
- **Then** I see confirmation "This will disable two-factor authentication for this user. They will only need their password to log in."
- **When** I confirm
- **Then** 2FA is disabled for the user
- **And** I see success message "2FA disabled for teacher.john"
- **And** an audit log entry is created

### AC3: Cannot Enable 2FA for Others
- **Given** user "teacher.john" has 2FA disabled
- **Then** I do NOT see an "Enable 2FA" button
- **And** I see note "Users must enable 2FA themselves from their profile"

### AC4: Cannot Modify Own 2FA Here
- **Given** I am viewing my own user record
- **Then** I see "Manage your 2FA from your profile settings"
- **And** 2FA enable/disable buttons are not shown

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Disable 2FA while user is in 2FA login step | Current login fails, user starts over without 2FA |
| Admin disables 2FA for Super Admin | Blocked if Admin (only Super Admin can) |
| Recovery codes after 2FA disable | Invalidated |
| Network error during disable | Show error, 2FA unchanged |

---

## Test Scenarios

- [ ] 2FA status displayed for each user
- [ ] Disable 2FA works with confirmation
- [ ] Cannot enable 2FA for others
- [ ] Cannot modify own 2FA from user management
- [ ] Audit log entry created on 2FA disable
- [ ] Admin cannot disable Super Admin 2FA
- [ ] Recovery codes invalidated on disable
- [ ] Confirmation required before disable

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0003](US0003-2fa-setup.md) | Functional | 2FA exists | Draft |
| [US0009](US0009-create-user.md) | Functional | Users exist | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
