# US0020: Regenerate Student QR Code

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to regenerate a student's QR code when their ID card is lost or damaged
**So that** the old QR code becomes invalid and a new one can be printed

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who handles lost ID card requests.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Regenerate QR Code Action
- **Given** I am on the student details page for "Maria Santos"
- **When** I click "Regenerate QR Code"
- **Then** I see confirmation "This will invalidate the current QR code. The student's old ID card will no longer work. Continue?"

### AC2: Old QR Code Invalidated
- **Given** I confirm QR code regeneration for "Maria Santos"
- **Then** the old QR code record is marked as IsValid = false
- **And** the old QR code record has InvalidatedAt set to current time

### AC3: New QR Code Generated
- **Given** I confirm QR code regeneration
- **Then** a new QR code is generated with:
  - New timestamp
  - New HMAC signature
  - IsValid = true
- **And** the new QR code is displayed
- **And** I see success message "QR code regenerated. Please print a new ID card."

### AC4: Scanner Rejects Old QR
- **Given** a student's QR code has been regenerated
- **When** the old QR code is scanned
- **Then** the scanner shows "QR code invalidated"
- **And** entry is denied

### AC5: Audit Log Entry
- **Given** I regenerate a QR code
- **Then** an audit log entry is created with:
  - Action: "QRCodeRegenerated"
  - StudentId
  - Details: "Old QR invalidated, new QR issued"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Regenerate for deactivated student | Blocked - student must be active |
| Multiple regenerations same day | Allow (lost card again) |
| Regenerate during active scan | Old scan completes, next fails |
| Network error during regenerate | Show error, no changes made |
| Cancel confirmation | No changes made |
| No existing QR code | Generate new (treat as first generation) |

---

## Test Scenarios

- [ ] Regenerate QR invalidates old code
- [ ] New QR code generated with new timestamp
- [ ] New QR code has new HMAC signature
- [ ] Old QR code rejected by scanner
- [ ] New QR code accepted by scanner
- [ ] Confirmation dialog shown
- [ ] Cancel preserves existing QR
- [ ] Audit log entry created
- [ ] Cannot regenerate for deactivated student
- [ ] Success message displayed

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0019](US0019-generate-qr.md) | Functional | QR generation logic | Draft |
| [US0015](US0015-create-student.md) | Functional | Students exist | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
