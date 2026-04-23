# US0025: Deactivate/Reactivate Faculty

> **Status:** Done
> **Epic:** [EP0004: Faculty Management](../epics/EP0004-faculty-management.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to deactivate faculty members who leave and reactivate them if they return
**So that** I can maintain accurate records without losing historical data

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who processes faculty departures and returns.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Deactivate Faculty Action
- **Given** I am on the faculty details page for active faculty "Juan Dela Cruz"
- **Then** I see a "Deactivate" button

### AC2: Deactivation Confirmation
- **Given** I click "Deactivate" on faculty "Juan Dela Cruz"
- **Then** I see confirmation "Are you sure you want to deactivate Juan Dela Cruz? They will be removed from the active faculty list."
- **And** I can confirm or cancel

### AC3: Deactivation Effect
- **Given** I confirm deactivation for "Juan Dela Cruz"
- **Then** the faculty record is set to IsActive = false
- **And** DeactivatedAt is set to current timestamp
- **And** I see success message "Faculty member deactivated"
- **And** the faculty no longer appears in the default faculty list

### AC4: Linked User Account Warning
- **Given** faculty "Juan Dela Cruz" is linked to a user account
- **When** I click "Deactivate"
- **Then** I see additional warning: "This faculty member has a linked user account. The user account will NOT be deactivated automatically."
- **And** I can choose to continue or cancel

### AC5: Reactivate Faculty Action
- **Given** I am viewing a deactivated faculty record
- **Then** I see a "Reactivate" button instead of "Deactivate"

### AC6: Reactivation Effect
- **Given** I click "Reactivate" on deactivated faculty "Juan Dela Cruz"
- **Then** the faculty record is set to IsActive = true
- **And** DeactivatedAt is cleared
- **And** I see success message "Faculty member reactivated"

### AC7: Audit Log Entries
- **Given** I deactivate or reactivate a faculty member
- **Then** an audit log entry is created with:
  - Action: "FacultyDeactivated" or "FacultyReactivated"
  - FacultyId
  - PerformedBy: my user ID

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Deactivate already deactivated | Button not visible |
| Reactivate already active | Button not visible |
| Network error | Show error, no state change |
| Cancel confirmation | No changes made |
| Faculty with linked user | Warn but allow deactivation |
| Concurrent deactivation | Check current state, show appropriate message |

---

## Test Scenarios

- [ ] Deactivate button visible for active faculty
- [ ] Confirmation dialog shown
- [ ] Deactivation sets IsActive = false
- [ ] DeactivatedAt timestamp set
- [ ] Success message displayed
- [ ] Faculty hidden from default list
- [ ] Reactivate button visible for deactivated faculty
- [ ] Reactivation sets IsActive = true
- [ ] DeactivatedAt cleared on reactivation
- [ ] Linked user account warning shown
- [ ] Audit log entries created
- [ ] Cancel preserves current state

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
