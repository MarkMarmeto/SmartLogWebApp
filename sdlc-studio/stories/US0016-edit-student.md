# US0016: Edit Student Details

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to edit existing student information
**So that** I can update records when student or parent details change

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who maintains student records.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Access Edit Student Form
- **Given** I am logged in as Admin Amy
- **When** I click "Edit" on a student in the student list
- **Then** I see a form pre-populated with the student's current details

### AC2: Successful Edit
- **Given** I am on the Edit Student form for "Maria Santos"
- **When** I change the Section from "A" to "B"
- **And** I click "Save Changes"
- **Then** the student's section is updated
- **And** I see success message "Student updated successfully"
- **And** an audit log entry is created

### AC3: Student ID Cannot Be Changed
- **Given** I am on the Edit Student form
- **Then** the Student ID field is read-only
- **And** I see note "Student ID cannot be changed"

### AC4: Parent Phone Update
- **Given** I am on the Edit Student form
- **When** I update the Parent Phone to a new valid number
- **And** I click "Save Changes"
- **Then** the phone number is updated
- **And** future SMS notifications will use the new number

### AC5: QR Code Not Affected by Edit
- **Given** I edit a student's name or other details
- **When** I save the changes
- **Then** the existing QR code remains valid
- **And** no new QR code is generated

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Edit deactivated student | Allow (may need to update before reactivating) |
| Change to invalid phone | Validation error, changes not saved |
| Concurrent edit by two admins | Last save wins, notify if possible |
| Clear required field | Validation error |
| Network error during save | Show error, preserve form data |
| Student moved to different grade/section | Update and log change |

---

## Test Scenarios

- [ ] Edit student name successfully
- [ ] Edit parent contact successfully
- [ ] Edit grade/section successfully
- [ ] Cannot change Student ID
- [ ] QR code unchanged after edit
- [ ] Validation errors for invalid data
- [ ] Audit log entry created on edit
- [ ] Deactivated student can be edited
- [ ] Phone validation still enforced
- [ ] Success message displayed

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0015](US0015-create-student.md) | Functional | Students to edit | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
