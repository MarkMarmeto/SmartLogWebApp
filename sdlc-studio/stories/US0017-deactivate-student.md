# US0017: Deactivate/Reactivate Student

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to deactivate students who leave the school and reactivate them if they return
**So that** former students cannot scan in but records are preserved

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who manages student enrollment lifecycle.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Deactivate Student
- **Given** I am viewing student "Maria Santos" who is active
- **When** I click "Deactivate"
- **Then** I see a dialog with:
  - Confirmation message: "Are you sure you want to deactivate this student? Their QR code will no longer work."
  - Deactivation Reason dropdown (required):
    - Graduated
    - Transferred Out
    - Dropped Out
    - Other (with text field for notes)
- **When** I select a reason and confirm
- **Then** the student's IsActive flag is set to false
- **And** DeactivationReason is stored
- **And** I see success message "Student deactivated"
- **And** an audit log entry is created with the reason

### AC2: Deactivated Student QR Code Invalid
- **Given** student "Maria Santos" has been deactivated
- **When** their QR code is scanned at the gate
- **Then** the scanner shows "Student deactivated" error
- **And** entry is denied

### AC3: Reactivate Student
- **Given** I am viewing deactivated student "Maria Santos"
- **When** I click "Reactivate"
- **Then** the student's IsActive flag is set to true
- **And** their existing QR code becomes valid again
- **And** I see success message "Student reactivated"
- **And** an audit log entry is created

### AC4: Deactivated Students Hidden by Default
- **Given** I am on the Student List page
- **Then** deactivated students are hidden by default
- **And** I can toggle "Show inactive" to see them

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Deactivate student mid-school-day | Immediate effect, next scan fails |
| Reactivate with expired QR (if expiry exists) | Generate new QR or extend expiry |
| Bulk deactivate (end of year) | Future enhancement |
| Deactivate student with unpaid fees | Allow (separate system concern) |
| Network error | Show error, no change made |

---

## Test Scenarios

- [ ] Deactivate requires reason selection
- [ ] Deactivation reason stored in database
- [ ] Deactivate student sets IsActive to false
- [ ] Deactivated student QR code rejected
- [ ] Reactivate student sets IsActive to true
- [ ] Reactivated student QR code works
- [ ] Deactivated students hidden by default in list
- [ ] Show inactive toggle reveals deactivated students
- [ ] Audit log entry on deactivate
- [ ] Audit log entry on reactivate
- [ ] Confirmation dialog before deactivation
- [ ] Cancel preserves active status

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Functional | Students to deactivate | Draft |
| [US0019](US0019-generate-qr.md) | Functional | QR validation | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Stakeholder Decisions

- [x] Add Deactivation Reason dropdown - **Approved by Registrar Rosa**
- [x] Reasons: Graduated, Transferred Out, Dropped Out, Other

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
| 2026-02-04 | Stakeholders | Added Deactivation Reason dropdown |
