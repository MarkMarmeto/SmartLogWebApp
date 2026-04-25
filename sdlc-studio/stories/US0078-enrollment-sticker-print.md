# US0078: Enrollment Sticker Print Page

> **Status:** Done
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to print annual enrollment stickers for students
**So that** I can apply them to the back of permanent ID cards each school year

## Context

### Background
Each year, students get a small sticker with current S.Y., Grade, Program, and Section. The sticker fits in one of the 4 slots on the card back. This is a new page separate from the card print page.

---

## Acceptance Criteria

### AC1: Sticker Print Page
- **Given** I navigate to `/Admin/PrintEnrollmentSticker`
- **Then** I see filters: Academic Year, Grade Level, Section
- **And** a "Generate Stickers" button

### AC2: Sticker Content
- **Given** student Maria Santos in S.Y. 2026-2027, Grade 8, Program STE, Section Ruby
- **Then** the sticker shows: "S.Y. 2026-2027 | Grade 8 | STE | Ruby"

### AC3: Sticker Size
- **Given** a generated sticker
- **Then** it is 75mm x 15mm (matches card back slot dimensions)

### AC4: Batch by Section
- **Given** I select Grade 7, Section "Ruby"
- **When** I click "Generate Stickers"
- **Then** stickers are generated for all active students in Grade 7 Ruby
- **And** arranged on a print page with cut lines (label sheet layout)

### AC5: Batch by Grade
- **Given** I select Grade 7 and no specific section
- **When** I click "Generate Stickers"
- **Then** stickers are generated for ALL students in Grade 7 (all sections)
- **And** grouped by section on the print page

### AC6: Current Academic Year Default
- **Given** the current academic year is 2026-2027
- **Then** the Academic Year filter defaults to 2026-2027

### AC7: Non-Graded Stickers
- **Given** a Non-Graded student in Level 2, Program REGULAR
- **Then** the sticker shows: "S.Y. 2026-2027 | Non-Graded | REGULAR | Level 2"

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| No students in selected section | "No students found for selected criteria" |
| Student not enrolled in current year | Not included in sticker batch |
| Program is REGULAR | Show "REGULAR" on sticker |
| Very long section name | Truncate at 20 characters with ellipsis |
| No academic year active | Error: "No active academic year" |

---

## Test Scenarios

- [ ] Sticker page loads with filters
- [ ] Sticker content correct (S.Y., Grade, Program, Section)
- [ ] Sticker size approximately 75mm x 15mm
- [ ] Batch by section generates correct stickers
- [ ] Batch by grade generates all sections
- [ ] Default academic year is current
- [ ] Non-Graded stickers work
- [ ] Print layout has cut lines

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0064](US0064-student-program-denormalization.md) | Data | Student.Program field | Draft |
| [US0060](US0060-section-program-mandatory.md) | Data | Section has Program | Draft |

---

## Estimation

**Story Points:** 3
**Complexity:** Medium

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
