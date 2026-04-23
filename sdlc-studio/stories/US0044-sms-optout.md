# US0044: SMS Opt-Out Management

> **Status:** Done
> **Epic:** [EP0007: SMS Notifications](../epics/EP0007-sms-notifications.md)
> **Owner:** TBD
> **Created:** 2026-02-04

## User Story

**As a** Admin Amy (Administrator)
**I want** to manage SMS opt-out preferences for students
**So that** parents who don't want notifications can be excluded

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages parent communication preferences.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: SMS Preference on Student Record
- **Given** I am viewing/editing a student record
- **Then** I see an SMS Notifications section with:
  - Toggle: "Send SMS to Parent" (default: ON)
  - Opt-out reason (if disabled)
  - Opted out date (if disabled)

### AC2: Disable SMS for Student
- **Given** I am editing student "Maria Santos"
- **When** I toggle "Send SMS to Parent" to OFF
- **Then** I must enter an opt-out reason:
  - Parent request
  - Invalid phone number
  - Other (with notes)
- **And** SMS notifications stop for this student

### AC3: Re-enable SMS for Student
- **Given** a student has SMS disabled
- **When** I toggle "Send SMS to Parent" to ON
- **Then** SMS notifications resume
- **And** opt-out reason is cleared

### AC4: Bulk SMS Management
- **Given** I am on the Student List
- **When** I select multiple students
- **Then** I can bulk enable/disable SMS notifications
- **And** I must provide a reason for bulk disable

### AC5: SMS Status in Student List
- **Given** I am viewing the Student List
- **Then** I see an "SMS" column with:
  - ✓ (green) if SMS enabled
  - ✗ (gray) if SMS disabled

### AC6: Filter by SMS Status
- **Given** I am on the Student List
- **When** I filter by "SMS Disabled"
- **Then** I see only students with SMS opt-out

### AC7: Audit Log for Opt-Out Changes
- **Given** I enable or disable SMS for a student
- **Then** an audit log entry is created:
  - Action: "SmsOptOutChanged"
  - StudentId
  - NewStatus: enabled/disabled
  - Reason (if disabled)

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Disable without reason | Show error "Please select a reason" |
| Parent calls to opt out | Admin can record on their behalf |
| Student transferred with opt-out | Preserve opt-out status |
| Bulk operation with mixed states | Show current state, allow change |
| Re-enable after parent request | Confirm "Parent previously opted out" |

---

## Test Scenarios

- [ ] SMS toggle visible on student record
- [ ] Disable requires reason
- [ ] Re-enable clears opt-out info
- [ ] Bulk enable/disable works
- [ ] SMS column shows in student list
- [ ] Filter by SMS disabled works
- [ ] Audit log created on change
- [ ] Opted-out students don't receive SMS
- [ ] Re-enabled students receive SMS again
- [ ] Opt-out reason stored correctly

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Functional | Student records exist | Ready |
| [US0016](US0016-edit-student.md) | Integration | Edit student form | Ready |
| [US0041](US0041-sms-queue.md) | Integration | Queue checks opt-out | Ready |

---

## Estimation

**Story Points:** 2
**Complexity:** Low-Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-04 | Claude | Initial story created |
