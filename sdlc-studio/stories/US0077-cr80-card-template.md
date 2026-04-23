# US0077: CR80 Card Template Redesign

> **Status:** Done
> **Epic:** [EP0013: QR Code Permanence & Card Redesign](../epics/EP0013-qr-permanence-card-redesign.md)
> **Owner:** TBD
> **Created:** 2026-04-16

## User Story

**As a** Admin Amy (Administrator)
**I want** to print permanent student ID cards in driver's license size without academic year
**So that** cards last the student's entire enrollment without annual reprinting

## Context

### Background
The current PrintQrCode page includes S.Y., Grade, and Section on the card — all year-specific. The redesigned card uses CR80 dimensions (85.6mm x 53.98mm), shows only permanent info (name, ID, LRN, photo, QR), and reserves the back for annual enrollment stickers.

---

## Acceptance Criteria

### AC1: Card Dimensions
- **Given** I print a student ID card
- **Then** the card template is CR80 standard: 85.6mm x 53.98mm (3.375" x 2.125")

### AC2: Card Front Layout
- **Given** a student "Maria Santos", ID "SL-2026-00001", LRN "123456789012"
- **Then** the card front shows:
  - School logo (top left)
  - School name (top)
  - Student photo (left side)
  - Student name: "Maria Santos"
  - Student ID: "SL-2026-00001"
  - LRN: "123456789012"
  - QR code (bottom left or right)
  - "SmartLog ID Card" label

### AC3: No Academic Year on Card
- **Given** the card front layout
- **Then** there is NO S.Y., Grade Level, Section, or Program displayed
- **And** these are reserved for the enrollment sticker on the back

### AC4: Card Back Layout
- **Given** the card back
- **Then** it shows 4 labeled sticker slots:
  - Slot 1, Slot 2, Slot 3, Slot 4
  - Each slot sized 75mm x 15mm
  - Label: "ENROLLMENT STICKER AREA"

### AC5: Batch Print
- **Given** I select multiple students (or a section/grade)
- **When** I click "Print ID Cards"
- **Then** a print-ready page generates with cards arranged for cutting
- **And** each page fits multiple cards (layout depends on paper size)

### AC6: Single Student Print
- **Given** I am on a student's detail page
- **When** I click "Print ID Card"
- **Then** a single card is generated in CR80 layout

### AC7: PrintQrCode Page Redesigned
- **Given** I navigate to `/Admin/PrintQrCode`
- **Then** the page uses the new CR80 card template
- **And** the old layout with S.Y. is replaced

---

## Edge Cases & Error Handling

| Scenario | Expected Behaviour |
|----------|-------------------|
| Student has no photo | Show placeholder silhouette |
| Student name too long for card | Truncate with ellipsis at 30 characters |
| LRN is null | Show "LRN: —" |
| School logo not configured | Show SmartLog default logo |
| Print on A4 paper | Cards arranged 2x4 per page with cut lines |

---

## Test Scenarios

- [ ] Card dimensions are CR80 (85.6mm x 53.98mm)
- [ ] Card front shows name, ID, LRN, photo, QR
- [ ] No academic year on card
- [ ] Card back has 4 sticker slots
- [ ] Batch print generates multi-card page
- [ ] Single student print works
- [ ] PrintQrCode page uses new layout
- [ ] Missing photo shows placeholder

---

## Dependencies

### Story Dependencies

| Story | Type | What's Needed | Status |
|-------|------|---------------|--------|
| [US0019](US0019-generate-qr.md) | Functional | QR code generation | Ready |
| [US0021](US0021-print-qr.md) | Functional | Existing print page to redesign | Ready |

---

## Estimation

**Story Points:** 5
**Complexity:** High

---

## Revision History

| Date | Author | Change |
|------|--------|--------|
| 2026-04-16 | Claude | Initial story created |
