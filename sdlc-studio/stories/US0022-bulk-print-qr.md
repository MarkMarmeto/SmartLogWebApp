# US0022: Bulk Print QR Codes

> **Status:** Done
> **Epic:** [EP0003: Student Management](../epics/EP0003-student-management.md)
> **Owner:** TBD
> **Created:** 2026-02-03

## User Story

**As a** Admin Amy (Administrator)
**I want** to print QR codes for an entire class or section as a PDF
**So that** I can efficiently create ID cards at the start of the school year

## Context

### Persona Reference
**Admin Amy** - Administrative Assistant who prepares ID cards for all students.
[Full persona details](../personas.md#2-admin-amy-school-administrator)

---

## Acceptance Criteria

### AC1: Bulk Print Option
- **Given** I am on the Student List page
- **When** I filter by Grade "5" and Section "A"
- **Then** I see a "Print All QR Codes" button

### AC2: PDF Generation
- **Given** I have filtered students to Grade 5, Section A (30 students)
- **When** I click "Print All QR Codes"
- **Then** a PDF is generated with all 30 students' QR codes
- **And** each student is on a separate section/card layout
- **And** I can download the PDF

### AC3: PDF Layout
- **Given** the PDF is generated
- **Then** each page contains 6-8 QR code cards (2x3 or 2x4 grid)
- **And** each card includes: QR code, Student Name, Student ID, Grade/Section

### AC4: Progress Indicator
- **Given** I click "Print All QR Codes" for a large class
- **Then** I see a progress indicator "Generating PDF... X of Y students"
- **And** I can cancel if needed

### AC5: Only Active Students
- **Given** some students in the filtered list are deactivated
- **Then** only active students are included in the PDF
- **And** I see a count "Generating QR codes for X active students"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No students in filter | Disable button, show "No students to print" |
| Very large class (500+) | Warn about file size, allow proceed |
| PDF generation fails | Show error, allow retry |
| Student without QR code | Generate QR first, then include |
| Network timeout | Show error, partial PDF not saved |
| Cancel during generation | Stop and discard partial PDF |
| All students deactivated | Show "No active students to print" |

---

## Test Scenarios

- [ ] Bulk print button visible with filters applied
- [ ] PDF generated successfully
- [ ] All filtered students included in PDF
- [ ] PDF layout is correct (6-8 per page)
- [ ] Each card has QR, name, ID, grade/section
- [ ] Progress indicator shown during generation
- [ ] Cancel button works
- [ ] Only active students included
- [ ] PDF download works
- [ ] QR codes in PDF are scannable

---

## Technical Notes

### PDF Generation
- Use QuestPDF or similar library for server-side PDF generation
- Consider async generation for large batches
- Cache PDF temporarily for download

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0019](US0019-generate-qr.md) | Functional | QR codes exist | Draft |
| [US0018](US0018-student-list.md) | Functional | Student filtering | Draft |

### External Dependencies

| Dependency | Type | Status |
|------------|------|--------|
| QuestPDF or similar | Library | Not Started |

---

## Estimation

**Story Points:** 5
**Complexity:** Medium-High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-02-03 | Claude | Initial story created |
