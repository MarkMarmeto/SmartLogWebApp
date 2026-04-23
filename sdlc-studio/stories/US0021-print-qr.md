# US0021: Print Individual QR Code

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to print an individual student's QR code
**So that** I can create their ID card

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who prints student ID cards.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Print QR Code Button
- **Given** I am on the student details page for "Maria Santos"
- **Then** I see a "Print QR Code" button

### AC2: Print Preview
- **Given** I click "Print QR Code"
- **Then** a print-friendly page opens with:
  - QR code image (large, scannable size)
  - Student name
  - Student ID
  - Grade and Section
  - School name

### AC3: Print Layout
- **Given** I am on the print preview
- **Then** the layout is designed for standard ID card size
- **And** QR code is at least 2cm x 2cm
- **And** text is clearly legible

### AC4: Browser Print
- **Given** I am on the print preview
- **When** I press Ctrl+P or click "Print"
- **Then** the browser print dialog opens
- **And** the page prints correctly

### AC5: Return to Student Details
- **Given** I am on the print preview
- **When** I click "Back" or "Close"
- **Then** I return to the student details page

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No printer connected | Browser handles (shows warning) |
| QR code image fails to load | Show error, retry option |
| Deactivated student | Print button disabled |
| Very long student name | Truncate with ellipsis |
| Print preview in mobile | Responsive layout, may suggest desktop |
| Browser blocks popup | Show link to open print page |

---

## Test Scenarios

- [ ] Print button visible on student details
- [ ] Print preview opens correctly
- [ ] QR code displayed at proper size
- [ ] Student info displayed correctly
- [ ] Browser print dialog opens
- [ ] Layout fits ID card size
- [ ] Back button returns to details
- [ ] Deactivated student cannot print
- [ ] QR code is scannable when printed
- [ ] Multiple browsers supported

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0019](US0019-generate-qr.md) | Functional | QR code exists | Draft |
| [US0015](US0015-create-student.md) | Functional | Student exists | Draft |

---

## Estimation

**Story Points:** 2
**Complexity:** Low

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
